// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef HAVE_MINIPAL_ERRNO_H
#define HAVE_MINIPAL_ERRNO_H

// Haiku uses different errno values
#if defined(TARGET_HAIKU)
#if !defined(EINVAL)
#define EINVAL ((int)0x80000005)
#endif
#if !defined(ERANGE)
#define ERANGE ((int)0x80007011)
#endif
#if !defined(EILSEQ)
#define EILSEQ ((int)0x80007026)
#endif
#if !defined(ENOENT)
#define ENOENT ((int)0x80006003)
#endif
#if !defined(EBADF)
#define EBADF ((int)0x80006000)
#endif
#if !defined(ENOMEM)
#define ENOMEM ((int)0x80000000)
#endif
#endif

#if !defined(EINVAL)
#define EINVAL          22
#endif
#if !defined(ERANGE)
#define ERANGE          34
#endif
#if !defined(EILSEQ)
#define EILSEQ          42
#endif
#if !defined(ENOENT)
#define ENOENT          2
#endif
#if !defined(EBADF)
#define EBADF           9
#endif
#if !defined(ENOMEM)
#define ENOMEM          12
#endif

#endif // HAVE_MINIPAL_ERRNO_H
