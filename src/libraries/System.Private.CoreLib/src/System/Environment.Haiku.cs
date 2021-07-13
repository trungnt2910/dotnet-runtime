// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System
{
    public static partial class Environment
    {
        public static unsafe long WorkingSet
        {
            get
            {
                long cookie = 0;
                long workingSet = 0;

                Interop.Process.area_info* areaInfo = Interop.Process.AllocAreaInfo();

                //fixed (ulong* cookiePtr = &cookie) {
                    while (Interop.Process.GetNextAreaInfo(ProcessId, &cookie, areaInfo) == 0)
                    {
                        workingSet += areaInfo->ram_size;
                    }
                //}

                Marshal.FreeHGlobal((IntPtr)areaInfo);

                return workingSet;
            }
        }
    }
}
