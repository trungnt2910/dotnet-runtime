// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

#pragma warning disable CA1823 // analyzer incorrectly flags fixed buffer length const (https://github.com/dotnet/roslyn/issues/37593)

internal static partial class Interop
{
    internal static partial class Process
    {
        private const int B_OS_NAME_LENGTH = 32;

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_TeamInfo", SetLastError = false)]
        private static extern unsafe int TeamInfo(int id, team_info *info, ulong size);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_NextAreaInfo", SetLastError = false)]
        private static extern unsafe int NextAreaInfo(int id, long *cookie, area_info *areaInfo, ulong size);

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct area_info
        {
            public int area;
            public fixed byte name[B_OS_NAME_LENGTH];
            public uint size;
            public uint @lock;
            public uint protection;
            public int team;
            public uint ram_size;
            public uint copy_count;
            public uint in_count;
            public uint out_count;
            public void* address;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct team_info
        {
            public int team;
            public int thread_count;
            public int image_count;
            public int debugger_nub_thread;
            public int debugger_nub_port;
            public int argc;
            public fixed byte args[64];
            public uint uid;
            public uint gid;
        }

        /// <summary>
        /// Gets information about a team (aka process)
        /// </summary>
        /// <param name="id">The team id.</param>
        public static unsafe team_info* GetTeamInfo(int id)
        {
            IntPtr handle = Marshal.AllocHGlobal(sizeof(team_info));
            team_info* teamInfo = (team_info*)handle;

            int status = TeamInfo(id, teamInfo, (ulong)sizeof(team_info));

            if (status != 0)
            {
                teamInfo = null;
                Marshal.FreeHGlobal(handle);
            }

            return teamInfo;
        }

        /// <summary>
        /// Gets information about an area
        /// </summary>
        /// <param name="team">The team id.</param>
        /// <param name="cookie">A pointer for iterating over areas.</param>
        /// <param name="info">The area_info to store retrieved info.</param>
        /// <returns>0 if successful.</returns>
        public static unsafe int GetNextAreaInfo(int team, long* cookie, area_info* info)
        {
            return NextAreaInfo(team, cookie, info, (ulong)sizeof(area_info));
        }

        public static unsafe area_info* AllocAreaInfo()
        {
            IntPtr handle = Marshal.AllocHGlobal(sizeof(area_info));

            return (area_info*)handle;
        }
    }
}
