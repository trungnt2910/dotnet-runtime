/**
 * \file
 * Interface to the dynamic linker
 *
 * Author:
 *    Mono Team (http://www.mono-project.com)
 *
 * Copyright 2001-2004 Ximian, Inc.
 * Copyright 2004-2009 Novell, Inc.
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */
#include "config.h"
#include "mono/utils/mono-compiler.h"
#include "mono/utils/mono-dl.h"
#include "mono/utils/mono-path.h"
#include "mono/utils/mono-threads-api.h"
#include "mono/utils/mono-error-internals.h"

#include <stdlib.h>
#include <stdio.h>
#include <ctype.h>
#include <string.h>
#include <glib.h>
#include <minipal/getexepath.h>

#if defined(TARGET_ANDROID) && !defined(WIN32)
#include <dlfcn.h>
#endif

// Contains LIBC_SO definition
#ifdef HAVE_GNU_LIB_NAMES_H
#include <gnu/lib-names.h>
#endif

struct MonoDlFallbackHandler {
	MonoDlFallbackLoad load_func;
	MonoDlFallbackSymbol symbol_func;
	MonoDlFallbackClose close_func;
	void *user_data;
};

static GSList *fallback_handlers;

static const char *
fix_libc_name (const char *name)
{
	if (name != NULL && strcmp (name, "libc") == 0) {
		// Taken from CoreCLR: https://github.com/dotnet/runtime/blob/732b7434a2ff453048c1647262c00bc8b7652112/src/coreclr/pal/src/loader/module.cpp#L569-L577
#if defined (HOST_DARWIN)
		return "/usr/lib/libc.dylib";
#elif defined (__FreeBSD__)
		return "libc.so.7";
#elif defined (HOST_HAIKU)
		return "libroot.so";
#elif defined (LIBC_SO)
		return LIBC_SO;
#else
		return "libc.so";
#endif
	}
	return name;
}

/**
 * mono_dl_open_self:
 * \param error pointer to MonoError
 *
 * Returns a handle to the main program, on Android it's not possible to
 * call dl_open(null) with RTLD_LAZY, it returns a null handle, so this
 * function uses RTLD_NOW.
 * handle in this platform.
 * \p error points to MonoError where an error will be stored in
 * case of failure.   The error needs to be cleared when done using it, \c mono_error_cleanup.
 * \returns a \c MonoDl pointer on success, NULL on failure.
 */
MonoDl*
mono_dl_open_self (MonoError *error)
{

#if defined(TARGET_ANDROID) && !defined(WIN32)
	MonoDl *module;
	module = (MonoDl *) g_malloc (sizeof (MonoDl));
	if (!module) {
		mono_error_set_out_of_memory (error, NULL);
		return NULL;
	}
	mono_refcount_init (module, NULL);
	module->handle = dlopen(NULL, RTLD_NOW);
	module->dl_fallback = NULL;
	module->full_name = NULL;
	return module;
#else
	return mono_dl_open (NULL, MONO_DL_LAZY, error);
#endif
}

/**
 * mono_dl_open:
 * \param name name of file containing shared module
 * \param flags flags
 * \param error pointer to MonoError
 *
 * Load the given file \p name as a shared library or dynamically loadable
 * module. \p name can be NULL to indicate loading the currently executing
 * binary image.
 * \p flags can have the \c MONO_DL_LOCAL bit set to avoid exporting symbols
 * from the module to the shared namespace. The \c MONO_DL_LAZY bit can be set
 * to lazily load the symbols instead of resolving everything at load time.
 * \p error points to MonoError where an error will be stored in
 * case of failure.   The error needs to be cleared when done using it, \c mono_error_cleanup.
 * \returns a \c MonoDl pointer on success, NULL on failure.
 */
MonoDl*
mono_dl_open (const char *name, int flags, MonoError *error)
{
	return mono_dl_open_full (name, flags, 0, error);
}

MonoDl *
mono_dl_open_full (const char *name, int mono_flags, int native_flags, MonoError *error)
{
	MonoDl *module;
	void *lib;
	MonoDlFallbackHandler *dl_fallback = NULL;
	int lflags = mono_dl_convert_flags (mono_flags, native_flags);
	char *found_name = NULL;

	module = (MonoDl *) g_malloc (sizeof (MonoDl));
	if (!module) {
		mono_error_set_out_of_memory (error, NULL);
		return NULL;
	}
	module->main_module = name == NULL? TRUE: FALSE;

	name = fix_libc_name (name);

	ERROR_DECL (load_error);

	// No GC safe transition because this is called early in main.c
	lib = mono_dl_open_file (name, lflags, load_error);

	if (!lib && !is_ok (load_error) && mono_error_get_error_code (load_error) == MONO_ERROR_BAD_IMAGE) {
		char *error_msg = mono_dl_current_error_string ();
		mono_error_set_error (error, MONO_ERROR_BAD_IMAGE, "%s", error_msg);
		g_free (error_msg);
		mono_error_cleanup (load_error);
		return NULL;
	}

	mono_error_cleanup (load_error);

	if (lib)
		found_name = g_strdup (name);

	if (!lib) {
		GSList *node;
		for (node = fallback_handlers; node != NULL; node = node->next){
			MonoDlFallbackHandler *handler = (MonoDlFallbackHandler *) node->data;
			char *error_msg = NULL;
			lib = handler->load_func (name, lflags, &error_msg, handler->user_data);
			g_free (error_msg);

			if (lib != NULL){
				dl_fallback = handler;
				found_name = g_strdup (name);
				break;
			}
		}
	}
	if (!lib && !dl_fallback) {
		/* This platform does not support dlopen */
		if (name == NULL) {
			g_free (module);
			mono_error_set_not_supported (error, NULL);
			return NULL;
		}

		char *error_msg = mono_dl_current_error_string ();
		mono_error_set_error (error, MONO_ERROR_FILE_NOT_FOUND, "%s", error_msg);
		g_free (error_msg);
		g_free (module);
		return NULL;
	}
	mono_refcount_init (module, NULL);
	module->handle = lib;
	module->dl_fallback = dl_fallback;
	module->full_name = found_name;
	return module;
}

/**
 * mono_dl_symbol:
 * \param module a MonoDl pointer
 * \param name symbol name
 * \param error pointer to MonoError
 * Load the address of symbol \p name from the given \p module.
 * \p error points to MonoError where an error will be stored in
 * case of failure.   The error needs to be cleared when done using it, \c mono_error_cleanup.
 * \returns address to symbol on success, NULL on failure
 */
void*
mono_dl_symbol (MonoDl *module, const char *name, MonoError *error)
{
	void *sym;
	char *err = NULL;

	if (module->dl_fallback) {
		sym = module->dl_fallback->symbol_func (module->handle, name, &err, module->dl_fallback->user_data);
	} else {
		sym = mono_dl_lookup_symbol (module, name);
	}

	if (sym) {
		mono_error_assert_ok (error);
		return sym;
	}

	err = (module->dl_fallback != NULL) ? err :  mono_dl_current_error_string ();
	mono_error_set_generic_error (error, "System", "EntryPointNotFoundException", "%s", err);
	g_free (err);

	return NULL;
}

/**
 * mono_dl_close:
 * \param module a \c MonoDl pointer
 * \param error pointer to a MonoError.
 * Unload the given module and free the module memory.
 * \p error points to a MonoError where an error will be stored in
 * case of failure.   The error needs to be cleared when done using it, \c mono_error_cleanup.
 * \returns \c 0 on success.
 */
void
mono_dl_close (MonoDl *module, MonoError *error)
{
	MonoDlFallbackHandler *dl_fallback = module->dl_fallback;

	if (dl_fallback){
		if (dl_fallback->close_func != NULL)
			dl_fallback->close_func (module->handle, dl_fallback->user_data);
	} else
		mono_dl_close_handle (module, error);

	g_free (module->full_name);
	g_free (module);
}

typedef gboolean (*dl_library_name_formatting_func)(int idx, gboolean *need_prefix, gboolean *need_suffix, const char **suffix);

static
gboolean
dl_default_library_name_formatting (int idx, gboolean *need_prefix, gboolean *need_suffix, const char **suffix)
{
	/*
	  The first time we are called, idx = 0 (as *iter is initialized to NULL). This is our
	  "bootstrap" phase in which we check the passed name verbatim and only if we fail to find
	  the dll thus named, we start appending suffixes, each time increasing idx twice (since now
	  the 0 value became special and we need to offset idx to a 0-based array index). This is
	  done to handle situations when mapped dll name is specified as libsomething.so.1 or
	  libsomething.so.1.1 or libsomething.so - testing it algorithmically would be an overkill
	  here.
	 */
	if (idx == 0) {
		/* Name */
		*need_prefix = FALSE;
		*need_suffix = FALSE;
		*suffix = "";
	} else if (idx == 1) {
		/* netcore system libs have a suffix but no prefix */
		*need_prefix = FALSE;
		*need_suffix = TRUE;
		*suffix = mono_dl_get_so_suffixes () [0];
	} else {
		/* Prefix.Name.suffix */
		*need_prefix = TRUE;
		*need_suffix = TRUE;
		*suffix = mono_dl_get_so_suffixes () [idx - 2];
		if ((*suffix) [0] == '\0')
			return FALSE;
	}

	return TRUE;
}

static
gboolean
dl_reverse_library_name_formatting (int idx, gboolean *need_prefix, gboolean *need_suffix, const char **suffix)
{
	const char ** suffixes = mono_dl_get_so_suffixes ();
	int suffix_count = 0;

	while (suffixes [suffix_count][0] != '\0')
		suffix_count++;

	if (idx < suffix_count) {
		/* Prefix.Name.suffix */
		*need_prefix = TRUE;
		*need_suffix = TRUE;
		*suffix = suffixes [idx];
	} else if (idx == suffix_count) {
		/* netcore system libs have a suffix but no prefix */
		*need_prefix = FALSE;
		*need_suffix = TRUE;
		*suffix = suffixes [0];
	} else if (idx == suffix_count + 1) {
		/* Name */
		*need_prefix = FALSE;
		*need_suffix = FALSE;
		*suffix = "";
	} else {
		return FALSE;
	}

	return TRUE;
}

static char*
dl_build_path (const char *directory, const char *name, void **iter, dl_library_name_formatting_func func)
{
	const char *prefix;
	const char *suffix;
	gboolean need_prefix = TRUE, need_suffix = TRUE;
	size_t prlen, suffixlen;
	char *res;
	int iteration;

	if (!iter)
		return NULL;


	iteration = GPOINTER_TO_UINT (*iter);
	if (!func (iteration, &need_prefix, &need_suffix, &suffix))
		return NULL;

	if (need_prefix) {
		prlen = strlen (mono_dl_get_so_prefix ());
		if (prlen && strncmp (name, mono_dl_get_so_prefix (), prlen) != 0)
			prefix = mono_dl_get_so_prefix ();
		else
			prefix = "";
	} else {
		prefix = "";
	}

	suffixlen = strlen (suffix);
	if (need_suffix && (suffixlen && strstr (name, suffix) == (name + strlen (name) - suffixlen)))
		suffix = "";

	if (directory && *directory)
		res = g_strconcat (directory, G_DIR_SEPARATOR_S, prefix, name, suffix, (const char*)NULL);
	else
		res = g_strconcat (prefix, name, suffix, (const char*)NULL);
	++iteration;
	*iter = GUINT_TO_POINTER (iteration);
	return res;
}

/**
 * mono_dl_build_path:
 * \param directory optional directory
 * \param name base name of the library
 * \param iter iterator token
 * Given a directory name and the base name of a library, iterate
 * over the possible file names of the library, taking into account
 * the possible different suffixes and prefixes on the host platform.
 *
 * The returned file name must be freed by the caller.
 * \p iter must point to a NULL pointer the first time the function is called
 * and then passed unchanged to the following calls.
 * \returns the filename or NULL at the end of the iteration
 */
char*
mono_dl_build_path (const char *directory, const char *name, void **iter)
{
#ifdef HOST_ANDROID
	return dl_build_path (directory, name, iter, dl_reverse_library_name_formatting);
#else
	return dl_build_path (directory, name, iter, dl_default_library_name_formatting);
#endif
}

static
gboolean
dl_prefix_suffix_library_name_formatting (int idx, gboolean *need_prefix, gboolean *need_suffix, const char **suffix)
{
	/* Prefix.Name.suffix */
	*need_prefix = TRUE;
	*need_suffix = TRUE;
	*suffix = mono_dl_get_so_suffixes () [idx];
	if ((*suffix) [0] == '\0')
		return FALSE;

	return TRUE;
}

/**
 * mono_dl_build_platform_path:
 * \param directory optional directory
 * \param name base name of the library
 * \param iter iterator token
 * Given a directory name and the base name of a library, iterate
 * over platform prefix and suffixes generating a library name
 * suitable for the platform. Function won't try additional fallbacks.
 *
 * The returned file name must be freed by the caller.
 * \p iter must point to a NULL pointer the first time the function is called
 * and then passed unchanged to the following calls.
 * \returns the filename or NULL at the end of the iteration
 */
char*
mono_dl_build_platform_path (const char *directory, const char *name, void **iter)
{
	return dl_build_path (directory, name, iter, dl_prefix_suffix_library_name_formatting);
}

MonoDlFallbackHandler *
mono_dl_fallback_register (MonoDlFallbackLoad load_func, MonoDlFallbackSymbol symbol_func, MonoDlFallbackClose close_func, void *user_data)
{
	MonoDlFallbackHandler *handler = NULL;
	if (load_func == NULL || symbol_func == NULL)
		goto leave;

	handler = g_new (MonoDlFallbackHandler, 1);
	handler->load_func = load_func;
	handler->symbol_func = symbol_func;
	handler->close_func = close_func;
	handler->user_data = user_data;

	fallback_handlers = g_slist_prepend (fallback_handlers, handler);

leave:
	return handler;
}

void
mono_dl_fallback_unregister (MonoDlFallbackHandler *handler)
{
	GSList *found;

	found = g_slist_find (fallback_handlers, handler);
	if (found == NULL)
		return;

	g_slist_remove (fallback_handlers, handler);
	g_free (handler);
}
