// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace System.Net.NetworkInformation
{
    // Haiku implementation of NetworkChange
    public partial class NetworkChange
    {
        // Lock controlling access to delegate subscriptions, socket, availability-changed state and timer.
        private static readonly object s_gate = new object();

        // The "leniency" window for NetworkAvailabilityChanged socket events.
        // All socket events received within this duration will be coalesced into a
        // single event. Generally, many route changed events are fired in succession,
        // and we are not interested in all of them, just the fact that network availability
        // has potentially changed as a result.
        private const int AvailabilityTimerWindowMilliseconds = 150;
        private static readonly TimerCallback s_availabilityTimerFiredCallback = OnAvailabilityTimerFired;
        private static Timer? s_availabilityTimer;
        private static bool s_availabilityHasChanged;

        private static IntPtr s_listener = IntPtr.Zero;

        [UnsupportedOSPlatform("illumos")]
        [UnsupportedOSPlatform("solaris")]
        public static event NetworkAddressChangedEventHandler? NetworkAddressChanged
        {
            add
            {
                if (value != null)
                {
                    lock (s_gate)
                    {
                        if (s_listener == IntPtr.Zero)
                        {
                            CreateListener();
                        }

                        s_addressChangedSubscribers.TryAdd(value, ExecutionContext.Capture());
                    }
                }
            }
            remove
            {
                if (value != null)
                {
                    lock (s_gate)
                    {
                        if (s_addressChangedSubscribers.Count == 0 && s_availabilityChangedSubscribers.Count == 0)
                        {
                            Debug.Assert(s_listener == IntPtr.Zero);
                            return;
                        }

                        s_addressChangedSubscribers.Remove(value);
                        if (s_addressChangedSubscribers.Count == 0 && s_availabilityChangedSubscribers.Count == 0)
                        {
                            DestroyListener();
                        }
                    }
                }
            }
        }

        [UnsupportedOSPlatform("illumos")]
        [UnsupportedOSPlatform("solaris")]
        public static event NetworkAvailabilityChangedEventHandler? NetworkAvailabilityChanged
        {
            add
            {
                if (value != null)
                {
                    lock (s_gate)
                    {
                        if (s_listener == IntPtr.Zero)
                        {
                            CreateListener();
                        }

                        if (s_availabilityTimer == null)
                        {
                            // Don't capture the current ExecutionContext and its AsyncLocals onto the timer causing them to live forever
                            using (ExecutionContext.SuppressFlow())
                            {
                                s_availabilityTimer = new Timer(s_availabilityTimerFiredCallback, null, Timeout.Infinite, Timeout.Infinite);
                            }
                        }

                        s_availabilityChangedSubscribers.TryAdd(value, ExecutionContext.Capture());
                    }
                }
            }
            remove
            {
                if (value != null)
                {
                    lock (s_gate)
                    {
                        if (s_addressChangedSubscribers.Count == 0 && s_availabilityChangedSubscribers.Count == 0)
                        {
                            Debug.Assert(s_listener == IntPtr.Zero);
                            return;
                        }

                        s_availabilityChangedSubscribers.Remove(value);
                        if (s_availabilityChangedSubscribers.Count == 0)
                        {
                            if (s_availabilityTimer != null)
                            {
                                s_availabilityTimer.Dispose();
                                s_availabilityTimer = null;
                                s_availabilityHasChanged = false;
                            }

                            if (s_addressChangedSubscribers.Count == 0)
                            {
                                DestroyListener();
                            }
                        }
                    }
                }
            }
        }

        private static unsafe void CreateListener()
        {
            Debug.Assert(Monitor.IsEntered(s_gate));
            Debug.Assert(s_listener == IntPtr.Zero);

            IntPtr listener;
            Interop.Error result = Interop.Sys.CreateNetworkChangeListenerSocket(&listener);
            if (result != Interop.Error.SUCCESS)
            {
                string message = Interop.Sys.GetLastErrorInfo().GetErrorMessage();
                throw new NetworkInformationException(message);
            }

            result = Interop.Sys.ReadEvents(listener, &ProcessEvent);
            if (result != Interop.Error.SUCCESS)
            {
                Interop.Sys.DestroyNetworkChangeListener(listener);
                string message = Interop.Sys.GetLastErrorInfo().GetErrorMessage();
                throw new NetworkInformationException(message);
            }

            s_listener = listener;
        }

        private static void DestroyListener()
        {
            Debug.Assert(Monitor.IsEntered(s_gate));
            Debug.Assert(s_listener != IntPtr.Zero);

            Interop.Sys.DestroyNetworkChangeListener(s_listener);
            s_listener = IntPtr.Zero;
        }

        [UnmanagedCallersOnly]
        private static void ProcessEvent(IntPtr socket, Interop.Sys.NetworkChangeKind kind)
        {
            if (kind != Interop.Sys.NetworkChangeKind.None)
            {
                lock (s_gate)
                {
                    if (s_listener != IntPtr.Zero)
                    {
                        OnListenerEvent(kind);
                    }
                }
            }
        }

        private static void OnListenerEvent(Interop.Sys.NetworkChangeKind kind)
        {
            switch (kind)
            {
                case Interop.Sys.NetworkChangeKind.AddressAdded:
                case Interop.Sys.NetworkChangeKind.AddressRemoved:
                    OnAddressChanged();
                    break;
                case Interop.Sys.NetworkChangeKind.AvailabilityChanged:
                    lock (s_gate)
                    {
                        if (s_availabilityTimer != null)
                        {
                            if (!s_availabilityHasChanged)
                            {
                                s_availabilityTimer.Change(AvailabilityTimerWindowMilliseconds, -1);
                            }
                            s_availabilityHasChanged = true;
                        }
                    }
                    break;
            }
        }

        private static void OnAddressChanged()
        {
            Dictionary<NetworkAddressChangedEventHandler, ExecutionContext?>? addressChangedSubscribers = null;

            lock (s_gate)
            {
                if (s_addressChangedSubscribers.Count > 0)
                {
                    addressChangedSubscribers = new Dictionary<NetworkAddressChangedEventHandler, ExecutionContext?>(s_addressChangedSubscribers);
                }
            }

            if (addressChangedSubscribers != null)
            {
                foreach (KeyValuePair<NetworkAddressChangedEventHandler, ExecutionContext?>
                    subscriber in addressChangedSubscribers)
                {
                    NetworkAddressChangedEventHandler handler = subscriber.Key;
                    ExecutionContext? ec = subscriber.Value;

                    if (ec == null) // Flow suppressed
                    {
                        handler(null, EventArgs.Empty);
                    }
                    else
                    {
                        ExecutionContext.Run(ec, s_runAddressChangedHandler, handler);
                    }
                }
            }
        }

        private static void OnAvailabilityTimerFired(object? state)
        {
            Dictionary<NetworkAvailabilityChangedEventHandler, ExecutionContext?>? availabilityChangedSubscribers = null;

            lock (s_gate)
            {
                if (s_availabilityHasChanged)
                {
                    s_availabilityHasChanged = false;
                    if (s_availabilityChangedSubscribers.Count > 0)
                    {
                        availabilityChangedSubscribers =
                            new Dictionary<NetworkAvailabilityChangedEventHandler, ExecutionContext?>(
                                s_availabilityChangedSubscribers);
                    }
                }
            }

            if (availabilityChangedSubscribers != null)
            {
                bool isAvailable = NetworkInterface.GetIsNetworkAvailable();
                NetworkAvailabilityEventArgs args = isAvailable ? s_availableEventArgs : s_notAvailableEventArgs;
                ContextCallback callbackContext = isAvailable ? s_runHandlerAvailable : s_runHandlerNotAvailable;

                foreach (KeyValuePair<NetworkAvailabilityChangedEventHandler, ExecutionContext?>
                    subscriber in availabilityChangedSubscribers)
                {
                    NetworkAvailabilityChangedEventHandler handler = subscriber.Key;
                    ExecutionContext? ec = subscriber.Value;

                    if (ec == null) // Flow suppressed
                    {
                        handler(null, args);
                    }
                    else
                    {
                        ExecutionContext.Run(ec, callbackContext, handler);
                    }
                }
            }
        }
    }
}
