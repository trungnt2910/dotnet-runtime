#include <signal.h>
#include <stddef.h>
#include "unwind_i.h"
#include "ucontext_i.h"

int
unw_is_signal_frame(unw_cursor_t *cursor)
{
	return X86_64_SCF_NONE;
}

HIDDEN int
x86_64_handle_signal_frame(unw_cursor_t *cursor)
{
	return UNW_EBADFRAME;
}

#ifndef UNW_REMOTE_ONLY
HIDDEN void *
x86_64_r_uc_addr(ucontext_t *uc, int reg)
{
	return NULL;
}

HIDDEN NORETURN void
x86_64_sigreturn(unw_cursor_t *cursor)
{
	abort();
}
#endif

