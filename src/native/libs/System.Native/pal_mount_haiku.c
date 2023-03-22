// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "pal_config.h"
#include "pal_mount.h"
#include "pal_utilities.h"
#include <assert.h>
#include <string.h>
#include <errno.h>
#include <limits.h>

int32_t SystemNative_GetAllMountPoints(MountPointFound onFound, void* context)
{
    return 0;
}

int32_t SystemNative_GetSpaceInfoForMountPoint(const char* name, MountPointInformation* mpi)
{
    return -1;
}

int32_t
SystemNative_GetFormatInfoForMountPoint(const char* name, char* formatNameBuffer, int32_t bufferLength, int64_t* formatType)
{
    *formatType = 0;

    return -1;
}
