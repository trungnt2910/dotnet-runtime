// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_config.h"
#include "pal_teaminfo.h"

#include "pal_errno.h"

#if __HAIKU__

#include <OS.h>

int32_t SystemNative_TeamInfo(int id, void* info, size_t size)
{
    return _get_team_info(id, (team_info*)info, size);
}

int32_t SystemNative_NextAreaInfo(int32_t id, int64_t* cookie, void* info, size_t size)
{
    return _get_next_area_info(id, cookie, (area_info*)info, size);
}

#else

int32_t SystemNative_TeamInfo(int id, void* info, size_t size)
{
    (void)id;
    (void)info;
    (void)size;
    errno = ENOTSUP;
    return -1;
}

int32_t SystemNative_NextAreaInfo(int32_t id, int64_t* cookie, void* info, size_t size)
{
    (void)id;
    (void)cookie;
    (void)info;
    (void)size;
    errno = ENOTSUP;
    return -1;
}

#endif
