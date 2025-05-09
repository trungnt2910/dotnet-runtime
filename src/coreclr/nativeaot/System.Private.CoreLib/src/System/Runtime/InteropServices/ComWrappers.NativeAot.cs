// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;

using Internal.Runtime;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Class for managing wrappers of COM IUnknown types.
    /// </summary>
    public abstract partial class ComWrappers
    {
        private const int TrackerRefShift = 32;
        private const ulong TrackerRefCounter = 1UL << TrackerRefShift;
        private const ulong DestroySentinel = 0x0000000080000000UL;
        private const ulong TrackerRefCountMask = 0xffffffff00000000UL;
        private const ulong ComRefCountMask = 0x000000007fffffffUL;
        private const int COR_E_ACCESSING_CCW = unchecked((int)0x80131544);

        internal static IntPtr DefaultIUnknownVftblPtr { get; } = CreateDefaultIUnknownVftbl();
        internal static IntPtr TaggedImplVftblPtr { get; } = CreateTaggedImplVftbl();
        internal static IntPtr DefaultIReferenceTrackerTargetVftblPtr { get; } = CreateDefaultIReferenceTrackerTargetVftbl();

        internal static readonly Guid IID_IUnknown = new Guid(0x00000000, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static readonly Guid IID_IReferenceTrackerTarget = new Guid(0x64bd43f8, 0xbfee, 0x4ec4, 0xb7, 0xeb, 0x29, 0x35, 0x15, 0x8d, 0xae, 0x21);
        internal static readonly Guid IID_TaggedImpl = new Guid(0x5c13e51c, 0x4f32, 0x4726, 0xa3, 0xfd, 0xf3, 0xed, 0xd6, 0x3d, 0xa3, 0xa0);
        internal static readonly Guid IID_IReferenceTracker = new Guid(0x11D3B13A, 0x180E, 0x4789, 0xA8, 0xBE, 0x77, 0x12, 0x88, 0x28, 0x93, 0xE6);
        internal static readonly Guid IID_IReferenceTrackerHost = new Guid(0x29a71c6a, 0x3c42, 0x4416, 0xa3, 0x9d, 0xe2, 0x82, 0x5a, 0x7, 0xa7, 0x73);
        internal static readonly Guid IID_IReferenceTrackerManager = new Guid(0x3cf184b4, 0x7ccb, 0x4dda, 0x84, 0x55, 0x7e, 0x6c, 0xe9, 0x9a, 0x32, 0x98);
        internal static readonly Guid IID_IFindReferenceTargetsCallback = new Guid(0x04b3486c, 0x4687, 0x4229, 0x8d, 0x14, 0x50, 0x5a, 0xb5, 0x84, 0xdd, 0x88);

        private static readonly Guid IID_IInspectable = new Guid(0xAF86E2E0, 0xB12D, 0x4c6a, 0x9C, 0x5A, 0xD7, 0xAA, 0x65, 0x10, 0x1E, 0x90);
        private static readonly Guid IID_IWeakReferenceSource = new Guid(0x00000038, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46);

        private static readonly ConditionalWeakTable<object, NativeObjectWrapper> s_nativeObjectWrapperTable = new ConditionalWeakTable<object, NativeObjectWrapper>();
        private static readonly GCHandleSet s_referenceTrackerNativeObjectWrapperCache = new GCHandleSet();

        private readonly ConditionalWeakTable<object, ManagedObjectWrapperHolder> _managedObjectWrapperTable = new ConditionalWeakTable<object, ManagedObjectWrapperHolder>();
        private readonly RcwCache _rcwCache = new();

        internal static bool TryGetComInstanceForIID(object obj, Guid iid, out IntPtr unknown, out long wrapperId)
        {
            if (obj == null
                || !s_nativeObjectWrapperTable.TryGetValue(obj, out NativeObjectWrapper? wrapper))
            {
                unknown = IntPtr.Zero;
                wrapperId = 0;
                return false;
            }

            wrapperId = wrapper.ComWrappers.id;
            return Marshal.QueryInterface(wrapper.ExternalComObject, iid, out unknown) == HResults.S_OK;
        }

        public static unsafe bool TryGetComInstance(object obj, out IntPtr unknown)
        {
            unknown = IntPtr.Zero;
            if (obj == null
                || !s_nativeObjectWrapperTable.TryGetValue(obj, out NativeObjectWrapper? wrapper))
            {
                return false;
            }

            return Marshal.QueryInterface(wrapper.ExternalComObject, IID_IUnknown, out unknown) == HResults.S_OK;
        }

        public static unsafe bool TryGetObject(IntPtr unknown, [NotNullWhen(true)] out object? obj)
        {
            obj = null;
            if (unknown == IntPtr.Zero)
            {
                return false;
            }

            ComInterfaceDispatch* comInterfaceDispatch = TryGetComInterfaceDispatch(unknown);
            if (comInterfaceDispatch == null ||
                ComInterfaceDispatch.ToManagedObjectWrapper(comInterfaceDispatch)->MarkedToDestroy)
            {
                return false;
            }

            obj = ComInterfaceDispatch.GetInstance<object>(comInterfaceDispatch);
            return true;
        }

        /// <summary>
        /// ABI for function dispatch of a COM interface.
        /// </summary>
        public unsafe partial struct ComInterfaceDispatch
        {
            /// <summary>
            /// Given a <see cref="System.IntPtr"/> from a generated Vtable, convert to the target type.
            /// </summary>
            /// <typeparam name="T">Desired type.</typeparam>
            /// <param name="dispatchPtr">Pointer supplied to Vtable function entry.</param>
            /// <returns>Instance of type associated with dispatched function call.</returns>
            public static unsafe T GetInstance<T>(ComInterfaceDispatch* dispatchPtr) where T : class
            {
                ManagedObjectWrapper* comInstance = ToManagedObjectWrapper(dispatchPtr);
                return Unsafe.As<T>(comInstance->Holder.WrappedObject);
            }

            internal static unsafe ManagedObjectWrapper* ToManagedObjectWrapper(ComInterfaceDispatch* dispatchPtr)
            {
                InternalComInterfaceDispatch* dispatch = (InternalComInterfaceDispatch*)unchecked((nuint)dispatchPtr & (nuint)InternalComInterfaceDispatch.DispatchAlignmentMask);
                return dispatch->_thisPtr;
            }
        }

        internal unsafe struct InternalComInterfaceDispatch
        {
#if TARGET_64BIT
            internal const int DispatchAlignment = 64;
            internal const int NumEntriesInDispatchTable = DispatchAlignment / 8 /* sizeof(void*) */  - 1;
#else
            internal const int DispatchAlignment = 16;
            internal const int NumEntriesInDispatchTable = DispatchAlignment / 4 /* sizeof(void*) */  - 1;
#endif
            internal const ulong DispatchAlignmentMask = unchecked((ulong)~(InternalComInterfaceDispatch.DispatchAlignment - 1));

            internal ManagedObjectWrapper* _thisPtr;

            public DispatchTable Vtables;

            [InlineArray(NumEntriesInDispatchTable)]
            internal unsafe struct DispatchTable
            {
                private IntPtr _element;
            }
        }

        internal enum CreateComInterfaceFlagsEx
        {
            None = 0,

            /// <summary>
            /// The caller will provide an IUnknown Vtable.
            /// </summary>
            /// <remarks>
            /// This is useful in scenarios when the caller has no need to rely on an IUnknown instance
            /// that is used when running managed code is not possible (i.e. during a GC). In traditional
            /// COM scenarios this is common, but scenarios involving <see href="https://learn.microsoft.com/windows/win32/api/windows.ui.xaml.hosting.referencetracker/nn-windows-ui-xaml-hosting-referencetracker-ireferencetrackertarget">Reference Tracker hosting</see>
            /// calling of the IUnknown API during a GC is possible.
            /// </remarks>
            CallerDefinedIUnknown = 1,

            /// <summary>
            /// Flag used to indicate the COM interface should implement <see href="https://learn.microsoft.com/windows/win32/api/windows.ui.xaml.hosting.referencetracker/nn-windows-ui-xaml-hosting-referencetracker-ireferencetrackertarget">IReferenceTrackerTarget</see>.
            /// When this flag is passed, the resulting COM interface will have an internal implementation of IUnknown
            /// and as such none should be supplied by the caller.
            /// </summary>
            TrackerSupport = 2,

            LacksICustomQueryInterface = 1 << 29,
            IsComActivated = 1 << 30,
            IsPegged = 1 << 31,

            InternalMask = IsPegged | IsComActivated | LacksICustomQueryInterface,
        }

        internal unsafe struct ManagedObjectWrapper
        {
            public volatile IntPtr HolderHandle; // This is GC Handle
            public ulong RefCount;

            public int UserDefinedCount;
            public ComInterfaceEntry* UserDefined;
            internal InternalComInterfaceDispatch* Dispatches;

            internal CreateComInterfaceFlagsEx Flags;

            public bool IsRooted
            {
                get
                {
                    ulong refCount = Interlocked.Read(ref RefCount);
                    bool rooted = GetComCount(refCount) > 0;
                    if (!rooted)
                    {
                        rooted = GetTrackerCount(refCount) > 0 &&
                            ((Flags & CreateComInterfaceFlagsEx.IsPegged) != 0 || TrackerObjectManager.s_isGlobalPeggingOn);
                    }
                    return rooted;
                }
            }

            public ManagedObjectWrapperHolder? Holder
            {
                get
                {
                    IntPtr handle = HolderHandle;
                    if (handle == IntPtr.Zero)
                        return null;
                    else
                        return Unsafe.As<ManagedObjectWrapperHolder>(GCHandle.FromIntPtr(handle).Target);
                }
            }

            public readonly bool MarkedToDestroy => IsMarkedToDestroy(RefCount);

            public uint AddRef()
            {
                return GetComCount(Interlocked.Increment(ref RefCount));
            }

            public uint Release()
            {
                Debug.Assert(GetComCount(RefCount) != 0);
                return GetComCount(Interlocked.Decrement(ref RefCount));
            }

            public uint AddRefFromReferenceTracker()
            {
                ulong prev;
                ulong curr;
                do
                {
                    prev = RefCount;
                    curr = prev + TrackerRefCounter;
                } while (Interlocked.CompareExchange(ref RefCount, curr, prev) != prev);

                return GetTrackerCount(curr);
            }

            public uint ReleaseFromReferenceTracker()
            {
                Debug.Assert(GetTrackerCount(RefCount) != 0);
                ulong prev;
                ulong curr;
                do
                {
                    prev = RefCount;
                    curr = prev - TrackerRefCounter;
                }
                while (Interlocked.CompareExchange(ref RefCount, curr, prev) != prev);

                // If we observe the destroy sentinel, then this release
                // must destroy the wrapper.
                if (curr == DestroySentinel)
                    Destroy();

                return GetTrackerCount(curr);
            }

            public uint Peg()
            {
                SetFlag(CreateComInterfaceFlagsEx.IsPegged);
                return HResults.S_OK;
            }

            public uint Unpeg()
            {
                ResetFlag(CreateComInterfaceFlagsEx.IsPegged);
                return HResults.S_OK;
            }


            public unsafe int QueryInterfaceForTracker(in Guid riid, out IntPtr ppvObject)
            {
                if (IsMarkedToDestroy(RefCount) || Holder is null)
                {
                    ppvObject = IntPtr.Zero;
                    return COR_E_ACCESSING_CCW;
                }

                return QueryInterface(in riid, out ppvObject);
            }

            public unsafe int QueryInterface(in Guid riid, out IntPtr ppvObject)
            {
                ppvObject = AsRuntimeDefined(in riid);
                if (ppvObject == IntPtr.Zero)
                {
                    if ((Flags & CreateComInterfaceFlagsEx.LacksICustomQueryInterface) == 0)
                    {
                        var customQueryInterface = Holder.WrappedObject as ICustomQueryInterface;
                        if (customQueryInterface is null)
                        {
                            SetFlag(CreateComInterfaceFlagsEx.LacksICustomQueryInterface);
                        }
                        else
                        {
                            Guid riidLocal = riid;
                            switch (customQueryInterface.GetInterface(ref riidLocal, out ppvObject))
                            {
                                case CustomQueryInterfaceResult.Handled:
                                    return HResults.S_OK;
                                case CustomQueryInterfaceResult.NotHandled:
                                    break;
                                case CustomQueryInterfaceResult.Failed:
                                    return HResults.COR_E_INVALIDCAST;
                            }
                        }
                    }

                    ppvObject = AsUserDefined(in riid);
                    if (ppvObject == IntPtr.Zero)
                        return HResults.COR_E_INVALIDCAST;
                }

                AddRef();
                return HResults.S_OK;
            }

            public IntPtr As(in Guid riid)
            {
                // Find target interface and return dispatcher or null if not found.
                IntPtr typeMaybe = AsRuntimeDefined(in riid);
                if (typeMaybe == IntPtr.Zero)
                    typeMaybe = AsUserDefined(in riid);

                return typeMaybe;
            }

            /// <returns>true if actually destroyed</returns>
            public unsafe bool Destroy()
            {
                Debug.Assert(GetComCount(RefCount) == 0 || HolderHandle == IntPtr.Zero);

                if (HolderHandle == IntPtr.Zero)
                {
                    // We either were previously destroyed or multiple ManagedObjectWrapperHolder
                    // were created by the ConditionalWeakTable for the same object and we lost the race.
                    return true;
                }

                ulong prev, refCount;
                do
                {
                    prev = RefCount;
                    refCount = prev | DestroySentinel;
                } while (Interlocked.CompareExchange(ref RefCount, refCount, prev) != prev);

                if (refCount == DestroySentinel)
                {
                    IntPtr handle = Interlocked.Exchange(ref HolderHandle, IntPtr.Zero);
                    if (handle != IntPtr.Zero)
                    {
                        RuntimeImports.RhHandleFree(handle);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private unsafe IntPtr GetDispatchPointerAtIndex(int index)
            {
                InternalComInterfaceDispatch* dispatch = &Dispatches[index / InternalComInterfaceDispatch.NumEntriesInDispatchTable];
                IntPtr* vtables = (IntPtr*)(void*)&dispatch->Vtables;
                return (IntPtr)(&vtables[index % InternalComInterfaceDispatch.NumEntriesInDispatchTable]);
            }

            private unsafe IntPtr AsRuntimeDefined(in Guid riid)
            {
                // The order of interface lookup here is important.
                // See CreateManagedObjectWrapper() for the expected order.
                int i = UserDefinedCount;
                if ((Flags & CreateComInterfaceFlagsEx.CallerDefinedIUnknown) == 0)
                {
                    if (riid == IID_IUnknown)
                    {
                        return GetDispatchPointerAtIndex(i);
                    }

                    i++;
                }

                if ((Flags & CreateComInterfaceFlagsEx.TrackerSupport) != 0)
                {
                    if (riid == IID_IReferenceTrackerTarget)
                    {
                        return GetDispatchPointerAtIndex(i);
                    }

                    i++;
                }

                {
                    if (riid == IID_TaggedImpl)
                    {
                        return GetDispatchPointerAtIndex(i);
                    }
                }

                return IntPtr.Zero;
            }

            private unsafe IntPtr AsUserDefined(in Guid riid)
            {
                for (int i = 0; i < UserDefinedCount; ++i)
                {
                    if (UserDefined[i].IID == riid)
                    {
                        return GetDispatchPointerAtIndex(i);
                    }
                }

                return IntPtr.Zero;
            }

            private void SetFlag(CreateComInterfaceFlagsEx flag)
            {
                int setMask = (int)flag;
                Interlocked.Or(ref Unsafe.As<CreateComInterfaceFlagsEx, int>(ref Flags), setMask);
            }

            private void ResetFlag(CreateComInterfaceFlagsEx flag)
            {
                int resetMask = ~(int)flag;
                Interlocked.And(ref Unsafe.As<CreateComInterfaceFlagsEx, int>(ref Flags), resetMask);
            }

            private static uint GetTrackerCount(ulong c)
            {
                return (uint)((c & TrackerRefCountMask) >> TrackerRefShift);
            }

            private static uint GetComCount(ulong c)
            {
                return (uint)(c & ComRefCountMask);
            }

            private static bool IsMarkedToDestroy(ulong c)
            {
                return (c & DestroySentinel) != 0;
            }
        }

        internal sealed unsafe class ManagedObjectWrapperHolder
        {
            static ManagedObjectWrapperHolder()
            {
                delegate* unmanaged<IntPtr, bool> callback = &IsRootedCallback;
                if (!RuntimeImports.RhRegisterRefCountedHandleCallback((nint)callback, MethodTable.Of<ManagedObjectWrapperHolder>()))
                {
                    throw new OutOfMemoryException();
                }
            }

            [UnmanagedCallersOnly]
            private static bool IsRootedCallback(IntPtr pObj)
            {
                // We are paused in the GC, so this is safe.
                ManagedObjectWrapperHolder* holder = (ManagedObjectWrapperHolder*)&pObj;
                return holder->_wrapper->IsRooted;
            }

            private readonly ManagedObjectWrapper* _wrapper;
            private readonly ManagedObjectWrapperReleaser _releaser;
            private readonly object _wrappedObject;

            public ManagedObjectWrapperHolder(ManagedObjectWrapper* wrapper, object wrappedObject)
            {
                _wrapper = wrapper;
                _wrappedObject = wrappedObject;
                _releaser = new ManagedObjectWrapperReleaser(wrapper);
                _wrapper->HolderHandle = RuntimeImports.RhHandleAllocRefCounted(this);
            }

            public unsafe IntPtr ComIp => _wrapper->As(in ComWrappers.IID_IUnknown);

            public object WrappedObject => _wrappedObject;

            public uint AddRef() => _wrapper->AddRef();
        }

        internal sealed unsafe class ManagedObjectWrapperReleaser
        {
            private ManagedObjectWrapper* _wrapper;

            public ManagedObjectWrapperReleaser(ManagedObjectWrapper* wrapper)
            {
                _wrapper = wrapper;
            }

            ~ManagedObjectWrapperReleaser()
            {
                IntPtr refCountedHandle = _wrapper->HolderHandle;
                if (refCountedHandle != IntPtr.Zero && RuntimeImports.RhHandleGet(refCountedHandle) != null)
                {
                    // The ManagedObjectWrapperHolder has not been fully collected, so it is still
                    // potentially reachable via the Conditional Weak Table.
                    // Keep ourselves alive in case the wrapped object is resurrected.
                    GC.ReRegisterForFinalize(this);
                    return;
                }

                // Release GC handle created when MOW was built.
                if (_wrapper->Destroy())
                {
                    NativeMemory.AlignedFree(_wrapper);
                    _wrapper = null;
                }
                else
                {
                    // There are still outstanding references on the COM side.
                    // This case should only be hit when an outstanding
                    // tracker refcount exists from AddRefFromReferenceTracker.
                    GC.ReRegisterForFinalize(this);
                }
            }
        }

        internal unsafe class NativeObjectWrapper
        {
            private IntPtr _externalComObject;
            private IntPtr _inner;
            private ComWrappers _comWrappers;
            private GCHandle _proxyHandle;
            private GCHandle _proxyHandleTrackingResurrection;
            private readonly bool _aggregatedManagedObjectWrapper;
            private readonly bool _uniqueInstance;

            static NativeObjectWrapper()
            {
                // Registering the weak reference support callbacks to enable
                // consulting ComWrappers when weak references are created
                // for RCWs.
                ComAwareWeakReference.InitializeCallbacks(&ComWeakRefToObject, &PossiblyComObject, &ObjectToComWeakRef);
            }

            public static NativeObjectWrapper Create(
                IntPtr externalComObject,
                IntPtr inner,
                ComWrappers comWrappers,
                object comProxy,
                CreateObjectFlags flags,
                ref IntPtr referenceTrackerMaybe)
            {
                if (flags.HasFlag(CreateObjectFlags.TrackerObject))
                {
                    IntPtr trackerObject = referenceTrackerMaybe;

                    // We're taking ownership of this reference tracker object, so reset the reference
                    referenceTrackerMaybe = IntPtr.Zero;

                    // If we already have a reference tracker (that will be the case in aggregation scenarios), then reuse it.
                    // Otherwise, do the 'QueryInterface' call for it here. This allows us to only ever query for this IID once.
                    if (trackerObject != IntPtr.Zero ||
                        Marshal.QueryInterface(externalComObject, IID_IReferenceTracker, out trackerObject) == HResults.S_OK)
                    {
                        return new ReferenceTrackerNativeObjectWrapper(externalComObject, inner, comWrappers, comProxy, flags, trackerObject);
                    }
                }

                return new NativeObjectWrapper(externalComObject, inner, comWrappers, comProxy, flags);
            }

            protected NativeObjectWrapper(IntPtr externalComObject, IntPtr inner, ComWrappers comWrappers, object comProxy, CreateObjectFlags flags)
            {
                _externalComObject = externalComObject;
                _inner = inner;
                _comWrappers = comWrappers;
                _uniqueInstance = flags.HasFlag(CreateObjectFlags.UniqueInstance);
                _proxyHandle = GCHandle.Alloc(comProxy, GCHandleType.Weak);

                // We have a separate handle tracking resurrection as we want to make sure
                // we clean up the NativeObjectWrapper only after the RCW has been finalized
                // due to it can access the native object in the finalizer. At the same time,
                // we want other callers which are using ProxyHandle such as the reference tracker runtime
                // to see the object as not alive once it is eligible for finalization.
                _proxyHandleTrackingResurrection = GCHandle.Alloc(comProxy, GCHandleType.WeakTrackResurrection);

                // If this is an aggregation scenario and the identity object
                // is a managed object wrapper, we need to call Release() to
                // indicate this external object isn't rooted. In the event the
                // object is passed out to native code an AddRef() must be called
                // based on COM convention and will "fix" the count.
                _aggregatedManagedObjectWrapper = flags.HasFlag(CreateObjectFlags.Aggregation) && TryGetComInterfaceDispatch(_externalComObject) != null;
                if (_aggregatedManagedObjectWrapper)
                {
                    Marshal.Release(externalComObject);
                }
            }

            internal IntPtr ExternalComObject => _externalComObject;
            internal ComWrappers ComWrappers => _comWrappers;
            internal GCHandle ProxyHandle => _proxyHandle;
            internal bool IsUniqueInstance => _uniqueInstance;
            internal bool IsAggregatedWithManagedObjectWrapper => _aggregatedManagedObjectWrapper;

            public virtual void Release()
            {
                if (!_uniqueInstance && _comWrappers is not null)
                {
                    _comWrappers._rcwCache.Remove(_externalComObject, this);
                    _comWrappers = null;
                }

                if (_proxyHandle.IsAllocated)
                {
                    _proxyHandle.Free();
                }

                if (_proxyHandleTrackingResurrection.IsAllocated)
                {
                    _proxyHandleTrackingResurrection.Free();
                }

                // If the inner was supplied, we need to release our reference.
                if (_inner != IntPtr.Zero)
                {
                    Marshal.Release(_inner);
                    _inner = IntPtr.Zero;
                }

                _externalComObject = IntPtr.Zero;
            }

            ~NativeObjectWrapper()
            {
                if (_proxyHandleTrackingResurrection.IsAllocated && _proxyHandleTrackingResurrection.Target != null)
                {
                    // The RCW object has not been fully collected, so it still
                    // can make calls on the native object in its finalizer.
                    // Keep ourselves alive until it is finalized.
                    GC.ReRegisterForFinalize(this);
                    return;
                }

                Release();
            }
        }

        internal sealed class ReferenceTrackerNativeObjectWrapper : NativeObjectWrapper
        {
            private IntPtr _trackerObject;
            private readonly bool _releaseTrackerObject;
            private int _trackerObjectDisconnected; // Atomic boolean, so using int.
            internal readonly IntPtr _contextToken;
            internal readonly GCHandle _nativeObjectWrapperWeakHandle;

            public IntPtr TrackerObject => (_trackerObject == IntPtr.Zero || _trackerObjectDisconnected == 1) ? IntPtr.Zero : _trackerObject;

            public ReferenceTrackerNativeObjectWrapper(
                nint externalComObject,
                nint inner,
                ComWrappers comWrappers,
                object comProxy,
                CreateObjectFlags flags,
                IntPtr trackerObject)
                : base(externalComObject, inner, comWrappers, comProxy, flags)
            {
                Debug.Assert(flags.HasFlag(CreateObjectFlags.TrackerObject));
                Debug.Assert(trackerObject != IntPtr.Zero);

                _trackerObject = trackerObject;
                _releaseTrackerObject = true;

                TrackerObjectManager.OnIReferenceTrackerFound(_trackerObject);
                TrackerObjectManager.AfterWrapperCreated(_trackerObject);

                if (flags.HasFlag(CreateObjectFlags.Aggregation))
                {
                    // Aggregation with an IReferenceTracker instance creates an extra AddRef()
                    // on the outer (e.g. MOW) so we clean up that issue here.
                    _releaseTrackerObject = false;
                    IReferenceTracker.ReleaseFromTrackerSource(_trackerObject); // IReferenceTracker
                    Marshal.Release(_trackerObject);
                }

                _contextToken = GetContextToken();
                _nativeObjectWrapperWeakHandle = GCHandle.Alloc(this, GCHandleType.Weak);
            }

            public override void Release()
            {
                // Remove the entry from the cache that keeps track of the active NativeObjectWrappers.
                if (_nativeObjectWrapperWeakHandle.IsAllocated)
                {
                    s_referenceTrackerNativeObjectWrapperCache.Remove(_nativeObjectWrapperWeakHandle);
                    _nativeObjectWrapperWeakHandle.Free();
                }

                DisconnectTracker();

                base.Release();
            }

            public void DisconnectTracker()
            {
                // Return if already disconnected or the tracker isn't set.
                if (_trackerObject == IntPtr.Zero || Interlocked.CompareExchange(ref _trackerObjectDisconnected, 1, 0) != 0)
                {
                    return;
                }

                // Always release the tracker source during a disconnect.
                // This to account for the implied IUnknown ownership by the runtime.
                IReferenceTracker.ReleaseFromTrackerSource(_trackerObject); // IUnknown

                // Disconnect from the tracker.
                if (_releaseTrackerObject)
                {
                    IReferenceTracker.ReleaseFromTrackerSource(_trackerObject); // IReferenceTracker
                    Marshal.Release(_trackerObject);
                    _trackerObject = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Globally registered instance of the ComWrappers class for reference tracker support.
        /// </summary>
        private static ComWrappers? s_globalInstanceForTrackerSupport;

        /// <summary>
        /// Globally registered instance of the ComWrappers class for marshalling.
        /// </summary>
        private static ComWrappers? s_globalInstanceForMarshalling;

        private static long s_instanceCounter;
        private readonly long id = Interlocked.Increment(ref s_instanceCounter);

        internal static object? GetOrCreateObjectFromWrapper(long wrapperId, IntPtr externalComObject)
        {
            if (s_globalInstanceForTrackerSupport != null && s_globalInstanceForTrackerSupport.id == wrapperId)
            {
                return s_globalInstanceForTrackerSupport.GetOrCreateObjectForComInstance(externalComObject, CreateObjectFlags.TrackerObject);
            }
            else if (s_globalInstanceForMarshalling != null && s_globalInstanceForMarshalling.id == wrapperId)
            {
                return ComObjectForInterface(externalComObject);
            }
            else
            {
                return null;
            }
        }

        // Custom type instead of a value tuple to avoid rooting 'ITuple' and other value tuple stuff
        private struct GetOrCreateComInterfaceForObjectParameters
        {
            public ComWrappers? This;
            public CreateComInterfaceFlags Flags;
        }

        /// <summary>
        /// Create a COM representation of the supplied object that can be passed to a non-managed environment.
        /// </summary>
        /// <param name="instance">The managed object to expose outside the .NET runtime.</param>
        /// <param name="flags">Flags used to configure the generated interface.</param>
        /// <returns>The generated COM interface that can be passed outside the .NET runtime.</returns>
        /// <remarks>
        /// If a COM representation was previously created for the specified <paramref name="instance" /> using
        /// this <see cref="ComWrappers" /> instance, the previously created COM interface will be returned.
        /// If not, a new one will be created.
        /// </remarks>
        public unsafe IntPtr GetOrCreateComInterfaceForObject(object instance, CreateComInterfaceFlags flags)
        {
            ArgumentNullException.ThrowIfNull(instance);

            ManagedObjectWrapperHolder managedObjectWrapper = _managedObjectWrapperTable.GetOrAdd(instance, static (c, items) =>
            {
                ManagedObjectWrapper* value = items.This!.CreateManagedObjectWrapper(c, items.Flags);
                return new ManagedObjectWrapperHolder(value, c);
            }, new GetOrCreateComInterfaceForObjectParameters { This = this, Flags = flags });

            managedObjectWrapper.AddRef();
            return managedObjectWrapper.ComIp;
        }

        private static nuint AlignUp(nuint value, nuint alignment)
        {
            nuint alignMask = alignment - 1;
            return (nuint)((value + alignMask) & ~alignMask);
        }

        private unsafe ManagedObjectWrapper* CreateManagedObjectWrapper(object instance, CreateComInterfaceFlags flags)
        {
            ComInterfaceEntry* userDefined = ComputeVtables(instance, flags, out int userDefinedCount);
            if ((userDefined == null && userDefinedCount != 0) || userDefinedCount < 0)
            {
                throw new ArgumentException();
            }

            // Maximum number of runtime supplied vtables.
            Span<IntPtr> runtimeDefinedVtable = stackalloc IntPtr[3];
            int runtimeDefinedCount = 0;

            // Check if the caller will provide the IUnknown table.
            if ((flags & CreateComInterfaceFlags.CallerDefinedIUnknown) == CreateComInterfaceFlags.None)
            {
                runtimeDefinedVtable[runtimeDefinedCount++] = DefaultIUnknownVftblPtr;
            }

            if ((flags & CreateComInterfaceFlags.TrackerSupport) != 0)
            {
                runtimeDefinedVtable[runtimeDefinedCount++] = DefaultIReferenceTrackerTargetVftblPtr;
            }

            {
                runtimeDefinedVtable[runtimeDefinedCount++] = TaggedImplVftblPtr;
            }

            // Compute size for ManagedObjectWrapper instance.
            int totalDefinedCount = runtimeDefinedCount + userDefinedCount;

            int numSections = totalDefinedCount / InternalComInterfaceDispatch.NumEntriesInDispatchTable;
            if (totalDefinedCount % InternalComInterfaceDispatch.NumEntriesInDispatchTable != 0)
            {
                // Account for a trailing partial section to fit all of the defined interfaces.
                numSections++;
            }

            nuint headerSize = AlignUp((nuint)sizeof(ManagedObjectWrapper), InternalComInterfaceDispatch.DispatchAlignment);

            // Instead of allocating a full section even when we have a trailing one, we'll allocate only
            // as much space as we need to store all of our dispatch tables.
            nuint dispatchSectionSize = (nuint)totalDefinedCount * (nuint)sizeof(void*) + (nuint)numSections * (nuint)sizeof(void*);

            // Allocate memory for the ManagedObjectWrapper with the correct alignment for our dispatch tables.
            IntPtr wrapperMem = (IntPtr)NativeMemory.AlignedAlloc(
                headerSize + dispatchSectionSize,
                InternalComInterfaceDispatch.DispatchAlignment);

            // Dispatches follow the ManagedObjectWrapper.
            InternalComInterfaceDispatch* pDispatches = (InternalComInterfaceDispatch*)((nuint)wrapperMem + headerSize);
            Span<InternalComInterfaceDispatch> dispatches = new Span<InternalComInterfaceDispatch>(pDispatches, numSections);
            for (int i = 0; i < dispatches.Length; i++)
            {
                dispatches[i]._thisPtr = (ManagedObjectWrapper*)wrapperMem;
                Span<IntPtr> dispatchVtables = dispatches[i].Vtables;
                for (int j = 0; j < dispatchVtables.Length; j++)
                {
                    int index = i * dispatchVtables.Length + j;
                    if (index >= totalDefinedCount)
                    {
                        break;
                    }
                    dispatchVtables[j] = (index < userDefinedCount) ? userDefined[index].Vtable : runtimeDefinedVtable[index - userDefinedCount];
                }
            }

            ManagedObjectWrapper* mow = (ManagedObjectWrapper*)wrapperMem;
            mow->HolderHandle = IntPtr.Zero;
            mow->RefCount = 0;
            mow->UserDefinedCount = userDefinedCount;
            mow->UserDefined = userDefined;
            mow->Flags = (CreateComInterfaceFlagsEx)flags;
            mow->Dispatches = pDispatches;
            return mow;
        }

        /// <summary>
        /// Get the currently registered managed object or creates a new managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// If a managed object was previously created for the specified <paramref name="externalComObject" />
        /// using this <see cref="ComWrappers" /> instance, the previously created object will be returned.
        /// If not, a new one will be created.
        /// </remarks>
        public object GetOrCreateObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags)
        {
            object? obj;
            if (!TryGetOrCreateObjectForComInstanceInternal(externalComObject, IntPtr.Zero, flags, null, out obj))
                throw new ArgumentNullException(nameof(externalComObject));

            return obj;
        }

        /// <summary>
        /// Get the currently registered managed object or uses the supplied managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapper">The <see cref="object"/> to be used as the wrapper for the external object</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// If the <paramref name="wrapper"/> instance already has an associated external object a <see cref="System.NotSupportedException"/> will be thrown.
        /// </remarks>
        public object GetOrRegisterObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags, object wrapper)
        {
            return GetOrRegisterObjectForComInstance(externalComObject, flags, wrapper, IntPtr.Zero);
        }

        /// <summary>
        /// Get the currently registered managed object or uses the supplied managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapper">The <see cref="object"/> to be used as the wrapper for the external object</param>
        /// <param name="inner">Inner for COM aggregation scenarios</param>
        /// <returns>Returns a managed object associated with the supplied external COM object.</returns>
        /// <remarks>
        /// This method override is for registering an aggregated COM instance with its associated inner. The inner
        /// will be released when the associated wrapper is eventually freed. Note that it will be released on a thread
        /// in an unknown apartment state. If the supplied inner is not known to be a free-threaded instance then
        /// it is advised to not supply the inner.
        ///
        /// If the <paramref name="wrapper"/> instance already has an associated external object a <see cref="System.NotSupportedException"/> will be thrown.
        /// </remarks>
        public object GetOrRegisterObjectForComInstance(IntPtr externalComObject, CreateObjectFlags flags, object wrapper, IntPtr inner)
        {
            ArgumentNullException.ThrowIfNull(wrapper);

            object? obj;
            if (!TryGetOrCreateObjectForComInstanceInternal(externalComObject, inner, flags, wrapper, out obj))
                throw new ArgumentNullException(nameof(externalComObject));

            return obj;
        }

        private static unsafe ComInterfaceDispatch* TryGetComInterfaceDispatch(IntPtr comObject)
        {
            // If the first Vtable entry is part of a ManagedObjectWrapper impl,
            // we know how to interpret the IUnknown.
            IntPtr knownQI = ((IntPtr*)((IntPtr*)comObject)[0])[0];
            if (knownQI != ((IntPtr*)DefaultIUnknownVftblPtr)[0]
                || knownQI != ((IntPtr*)DefaultIReferenceTrackerTargetVftblPtr)[0])
            {
                // It is possible the user has defined their own IUnknown impl so
                // we fallback to the tagged interface approach to be sure.
                if (0 != Marshal.QueryInterface(comObject, IID_TaggedImpl, out nint implMaybe))
                {
                    return null;
                }

                IntPtr currentVersion = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&ITaggedImpl_IsCurrentVersion;
                int hr = ((delegate* unmanaged<IntPtr, IntPtr, int>)(*(*(void***)implMaybe + 3 /* ITaggedImpl.IsCurrentVersion slot */)))(implMaybe, currentVersion);
                Marshal.Release(implMaybe);
                if (hr != 0)
                {
                    return null;
                }
            }

            return (ComInterfaceDispatch*)comObject;
        }

        private static void DetermineIdentityAndInner(
            IntPtr externalComObject,
            IntPtr innerMaybe,
            CreateObjectFlags flags,
            out IntPtr identity,
            out IntPtr inner,
            out IntPtr referenceTrackerMaybe)
        {
            inner = innerMaybe;

            IntPtr checkForIdentity = externalComObject;

            // Check if the flags indicate we are creating
            // an object for an external IReferenceTracker instance
            // that we are aggregating with.
            bool refTrackerInnerScenario = flags.HasFlag(CreateObjectFlags.TrackerObject)
                && flags.HasFlag(CreateObjectFlags.Aggregation);
            if (refTrackerInnerScenario &&
                Marshal.QueryInterface(externalComObject, IID_IReferenceTracker, out IntPtr referenceTrackerPtr) == HResults.S_OK)
            {
                // We are checking the supplied external value
                // for IReferenceTracker since in .NET 5 API usage scenarios
                // this could actually be the inner and we want the true identity
                // not the inner . This is a trick since the only way
                // to get identity from an inner is through a non-IUnknown
                // interface QI. Once we have the IReferenceTracker
                // instance we can be sure the QI for IUnknown will really
                // be the true identity. This allows us to keep the reference tracker
                // reference alive, so we can reuse it later.
                checkForIdentity = referenceTrackerPtr;
                referenceTrackerMaybe = referenceTrackerPtr;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(checkForIdentity, IID_IUnknown, out identity));
            }
            else
            {
                referenceTrackerMaybe = IntPtr.Zero;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(externalComObject, IID_IUnknown, out identity));
            }

            // Set the inner if scenario dictates an update.
            if (innerMaybe == IntPtr.Zero &&               // User didn't supply inner - .NET 5 API scenario sanity check.
                checkForIdentity != externalComObject &&   // Target of check was changed - .NET 5 API scenario sanity check.
                externalComObject != identity &&           // The supplied object doesn't match the computed identity.
                refTrackerInnerScenario)                   // The appropriate flags were set.
            {
                inner = externalComObject;
            }
        }

        /// <summary>
        /// Get the currently registered managed object or creates a new managed object and registers it.
        /// </summary>
        /// <param name="externalComObject">Object to import for usage into the .NET runtime.</param>
        /// <param name="innerMaybe">The inner instance if aggregation is involved</param>
        /// <param name="flags">Flags used to describe the external object.</param>
        /// <param name="wrapperMaybe">The <see cref="object"/> to be used as the wrapper for the external object.</param>
        /// <param name="retValue">The managed object associated with the supplied external COM object or <c>null</c> if it could not be created.</param>
        /// <returns>Returns <c>true</c> if a managed object could be retrieved/created, <c>false</c> otherwise</returns>
        private unsafe bool TryGetOrCreateObjectForComInstanceInternal(
            IntPtr externalComObject,
            IntPtr innerMaybe,
            CreateObjectFlags flags,
            object? wrapperMaybe,
            [NotNullWhen(true)] out object? retValue)
        {
            if (externalComObject == IntPtr.Zero)
                throw new ArgumentNullException(nameof(externalComObject));

            if (innerMaybe != IntPtr.Zero && !flags.HasFlag(CreateObjectFlags.Aggregation))
                throw new InvalidOperationException(SR.InvalidOperation_SuppliedInnerMustBeMarkedAggregation);

            DetermineIdentityAndInner(
                externalComObject,
                innerMaybe,
                flags,
                out IntPtr identity,
                out IntPtr inner,
                out IntPtr referenceTrackerMaybe);

            try
            {
                // If the user has requested a unique instance,
                // we will immediately create the object, register it,
                // and return.
                if (flags.HasFlag(CreateObjectFlags.UniqueInstance))
                {
                    retValue = CreateAndRegisterObjectForComInstance(identity, inner, flags, ref referenceTrackerMaybe);
                    return retValue is not null;
                }

                // If we have a live cached wrapper currently,
                // return that.
                if (_rcwCache.FindProxyForComInstance(identity) is object liveCachedWrapper)
                {
                    retValue = liveCachedWrapper;
                    return true;
                }

                // If the user tried to provide a pre-created managed wrapper, try to register
                // that object as the wrapper.
                if (wrapperMaybe is not null)
                {
                    retValue = RegisterObjectForComInstance(identity, inner, wrapperMaybe, flags, ref referenceTrackerMaybe);
                    return retValue is not null;
                }

                // Check if the provided COM instance is actually a managed object wrapper from this
                // ComWrappers instance, and use it if it is.
                if (flags.HasFlag(CreateObjectFlags.Unwrap))
                {
                    ComInterfaceDispatch* comInterfaceDispatch = TryGetComInterfaceDispatch(identity);
                    if (comInterfaceDispatch != null)
                    {
                        // If we found a managed object wrapper in this ComWrappers instance
                        // and it has the same identity pointer as the one we're creating a NativeObjectWrapper for,
                        // unwrap it. We don't AddRef the wrapper as we don't take a reference to it.
                        //
                        // A managed object can have multiple managed object wrappers, with a max of one per context.
                        // Let's say we have a managed object A and ComWrappers instances C1 and C2. Let B1 and B2 be the
                        // managed object wrappers for A created with C1 and C2 respectively.
                        // If we are asked to create an EOC for B1 with the unwrap flag on the C2 ComWrappers instance,
                        // we will create a new wrapper. In this scenario, we'll only unwrap B2.
                        object unwrapped = ComInterfaceDispatch.GetInstance<object>(comInterfaceDispatch);
                        if (_managedObjectWrapperTable.TryGetValue(unwrapped, out ManagedObjectWrapperHolder? unwrappedWrapperInThisContext))
                        {
                            // The unwrapped object has a CCW in this context. Compare with identity
                            // so we can see if it's the CCW for the unwrapped object in this context.
                            if (unwrappedWrapperInThisContext.ComIp == identity)
                            {
                                retValue = unwrapped;
                                return true;
                            }
                        }
                    }
                }

                // If the user didn't provide a wrapper and couldn't unwrap a managed object wrapper,
                // create a new wrapper.
                retValue = CreateAndRegisterObjectForComInstance(identity, inner, flags, ref referenceTrackerMaybe);
                return retValue is not null;
            }
            finally
            {
                // Releasing a native object can never throw (it's a native call, so exceptions can't
                // go through the ABI, it'd just crash the whole process). So we can use a single
                // 'finally' block to release both native pointers we're holding in this scope.
                Marshal.Release(identity);

                if (referenceTrackerMaybe != IntPtr.Zero)
                {
                    Marshal.Release(referenceTrackerMaybe);
                }
            }
        }

        private object? CreateAndRegisterObjectForComInstance(
            IntPtr identity,
            IntPtr inner,
            CreateObjectFlags flags,
            ref IntPtr referenceTrackerMaybe)
        {
            object? retValue = CreateObject(identity, flags);
            if (retValue is null)
            {
                // If ComWrappers instance cannot create wrapper, we can do nothing here.
                return null;
            }

            return RegisterObjectForComInstance(identity, inner, retValue, flags, ref referenceTrackerMaybe);
        }

        private object RegisterObjectForComInstance(
            IntPtr identity,
            IntPtr inner,
            object comProxy,
            CreateObjectFlags flags,
            ref IntPtr referenceTrackerMaybe)
        {
            NativeObjectWrapper nativeObjectWrapper = NativeObjectWrapper.Create(
                identity,
                inner,
                this,
                comProxy,
                flags,
                ref referenceTrackerMaybe);

            object actualProxy = comProxy;
            NativeObjectWrapper actualWrapper = nativeObjectWrapper;
            if (!nativeObjectWrapper.IsUniqueInstance)
            {
                // Add our entry to the cache here, using an already existing entry if someone else beat us to it.
                (actualWrapper, actualProxy) = _rcwCache.GetOrAddProxyForComInstance(identity, nativeObjectWrapper, comProxy);
                if (actualWrapper != nativeObjectWrapper)
                {
                    // We raced with another thread to map identity to nativeObjectWrapper
                    // and lost the race. We will use the other thread's nativeObjectWrapper, so we can release ours.
                    nativeObjectWrapper.Release();
                }
            }

            // At this point, actualProxy is the RCW object for the identity
            // and actualWrapper is the NativeObjectWrapper that is in the RCW cache (if not unique) that associates the identity with actualProxy.
            // Register the NativeObjectWrapper to handle lifetime tracking of the references to the COM object.
            RegisterWrapperForObject(actualWrapper, actualProxy);

            return actualProxy;
        }

        private void RegisterWrapperForObject(NativeObjectWrapper wrapper, object comProxy)
        {
            // When we call into RegisterWrapperForObject, there is only one valid non-"unique instance" wrapper for a given
            // COM instance, which is already registered in the RCW cache.
            // If we find a wrapper in the table that is a different NativeObjectWrapper instance
            // then it must be for a different COM instance.
            // It's possible that we could race here with another thread that is trying to register the same comProxy
            // for the same COM instance, but in that case we'll be passed the same NativeObjectWrapper instance
            // for both threads. In that case, it doesn't matter which thread adds the entry to the NativeObjectWrapper table
            // as the entry is always the same pair.
            Debug.Assert(wrapper.ProxyHandle.Target == comProxy);
            Debug.Assert(wrapper.IsUniqueInstance || _rcwCache.FindProxyForComInstance(wrapper.ExternalComObject) == comProxy);

            // Add the input wrapper bound to the COM proxy, if there isn't one already. If another thread raced
            // against this one and this lost, we'd get the wrapper added from that thread instead.
            NativeObjectWrapper registeredWrapper = s_nativeObjectWrapperTable.GetOrAdd(comProxy, wrapper);

            // We lost the race, so we cannot register the incoming wrapper with the target object
            if (registeredWrapper != wrapper)
            {
                Debug.Assert(registeredWrapper.ExternalComObject != wrapper.ExternalComObject);
                wrapper.Release();
                throw new NotSupportedException();
            }

            // Always register our wrapper to the reference tracker handle cache here.
            // We may not be the thread that registered the handle, but we need to ensure that the wrapper
            // is registered before we return to user code. Otherwise the wrapper won't be walked by the
            // TrackerObjectManager and we could end up missing a section of the object graph.
            // This cache deduplicates, so it is okay that the wrapper will be registered multiple times.
            AddWrapperToReferenceTrackerHandleCache(registeredWrapper);
        }

        private static void AddWrapperToReferenceTrackerHandleCache(NativeObjectWrapper wrapper)
        {
            if (wrapper is ReferenceTrackerNativeObjectWrapper referenceTrackerNativeObjectWrapper)
            {
                s_referenceTrackerNativeObjectWrapperCache.Add(referenceTrackerNativeObjectWrapper._nativeObjectWrapperWeakHandle);
            }
        }

        private sealed class RcwCache
        {
            private readonly Lock _lock = new Lock(useTrivialWaits: true);
            private readonly Dictionary<object, GCHandle> _cache = [];

            /// <summary>
            /// Gets the current RCW proxy object for <paramref name="comPointer"/> if it exists in the cache or inserts a new entry with <paramref name="comProxy"/>.
            /// </summary>
            /// <param name="comPointer">The com instance we want to get or record an RCW for.</param>
            /// <param name="wrapper">The <see cref="NativeObjectWrapper"/> for <paramref name="comProxy"/>.</param>
            /// <param name="comProxy">The proxy object that is associated with <paramref name="wrapper"/>.</param>
            /// <returns>The proxy object currently in the cache for <paramref name="comPointer"/> or the proxy object owned by <paramref name="wrapper"/> if no entry exists and the corresponding native wrapper.</returns>
            public (NativeObjectWrapper actualWrapper, object actualProxy) GetOrAddProxyForComInstance(IntPtr comPointer, NativeObjectWrapper wrapper, object comProxy)
            {
                lock (_lock)
                {
                    Debug.Assert(wrapper.ProxyHandle.Target == comProxy);
                    ref GCHandle rcwEntry = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, comPointer, out bool exists);
                    if (!exists)
                    {
                        // Someone else didn't beat us to adding the entry to the cache.
                        // Add our entry here.
                        rcwEntry = GCHandle.Alloc(wrapper, GCHandleType.Weak);
                    }
                    else if (rcwEntry.Target is not (NativeObjectWrapper cachedWrapper))
                    {
                        Debug.Assert(rcwEntry.IsAllocated);
                        // The target was collected, so we need to update the cache entry.
                        rcwEntry.Target = wrapper;
                    }
                    else
                    {
                        object? existingProxy = cachedWrapper.ProxyHandle.Target;
                        // The target NativeObjectWrapper was not collected, but we need to make sure
                        // that the proxy object is still alive.
                        if (existingProxy is not null)
                        {
                            // The existing proxy object is still alive, we will use that.
                            return (cachedWrapper, existingProxy);
                        }

                        // The proxy object was collected, so we need to update the cache entry.
                        rcwEntry.Target = wrapper;
                    }

                    // We either added an entry to the cache or updated an existing entry that was dead.
                    // Return our target object.
                    return (wrapper, comProxy);
                }
            }

            public object? FindProxyForComInstance(IntPtr comPointer)
            {
                lock (_lock)
                {
                    if (_cache.TryGetValue(comPointer, out GCHandle existingHandle))
                    {
                        if (existingHandle.Target is NativeObjectWrapper { ProxyHandle.Target: object cachedProxy })
                        {
                            // The target exists and is still alive. Return it.
                            return cachedProxy;
                        }

                        // The target was collected, so we need to remove the entry from the cache.
                        _cache.Remove(comPointer);
                        existingHandle.Free();
                    }

                    return null;
                }
            }

            public void Remove(IntPtr comPointer, NativeObjectWrapper wrapper)
            {
                lock (_lock)
                {
                    // TryGetOrCreateObjectForComInstanceInternal may have put a new entry into the cache
                    // in the time between the GC cleared the contents of the GC handle but before the
                    // NativeObjectWrapper finalizer ran.
                    // Only remove the entry if the target of the GC handle is the NativeObjectWrapper
                    // or is null (indicating that the corresponding NativeObjectWrapper has been scheduled for finalization).
                    if (_cache.TryGetValue(comPointer, out GCHandle cachedRef)
                        && (wrapper == cachedRef.Target
                            || cachedRef.Target is null))
                    {
                        _cache.Remove(comPointer);
                        cachedRef.Free();
                    }
                }
            }
        }

        /// <summary>
        /// Register a <see cref="ComWrappers" /> instance to be used as the global instance for reference tracker support.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        /// <remarks>
        /// This function can only be called a single time. Subsequent calls to this function will result
        /// in a <see cref="System.InvalidOperationException"/> being thrown.
        ///
        /// Scenarios where this global instance may be used are:
        ///  * Object tracking via the <see cref="CreateComInterfaceFlags.TrackerSupport" /> and <see cref="CreateObjectFlags.TrackerObject" /> flags.
        /// </remarks>
        public static void RegisterForTrackerSupport(ComWrappers instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (null != Interlocked.CompareExchange(ref s_globalInstanceForTrackerSupport, instance, null))
            {
                throw new InvalidOperationException(SR.InvalidOperation_ResetGlobalComWrappersInstance);
            }
        }

        /// <summary>
        /// Register a <see cref="ComWrappers" /> instance to be used as the global instance for marshalling in the runtime.
        /// </summary>
        /// <param name="instance">Instance to register</param>
        /// <remarks>
        /// This function can only be called a single time. Subsequent calls to this function will result
        /// in a <see cref="System.InvalidOperationException"/> being thrown.
        ///
        /// Scenarios where this global instance may be used are:
        ///  * Usage of COM-related Marshal APIs
        ///  * P/Invokes with COM-related types
        ///  * COM activation
        /// </remarks>
        [SupportedOSPlatformAttribute("windows")]
        public static void RegisterForMarshalling(ComWrappers instance)
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (null != Interlocked.CompareExchange(ref s_globalInstanceForMarshalling, instance, null))
            {
                throw new InvalidOperationException(SR.InvalidOperation_ResetGlobalComWrappersInstance);
            }
        }

        /// <summary>
        /// Get the runtime provided IUnknown implementation.
        /// </summary>
        /// <param name="fpQueryInterface">Function pointer to QueryInterface.</param>
        /// <param name="fpAddRef">Function pointer to AddRef.</param>
        /// <param name="fpRelease">Function pointer to Release.</param>
        public static unsafe void GetIUnknownImpl(out IntPtr fpQueryInterface, out IntPtr fpAddRef, out IntPtr fpRelease)
        {
            fpQueryInterface = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&ComWrappers.IUnknown_QueryInterface;
            fpAddRef = (IntPtr)(delegate*<IntPtr, uint>)&RuntimeImports.RhIUnknown_AddRef; // Implemented in C/C++ to avoid GC transitions
            fpRelease = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IUnknown_Release;
        }

        internal static IntPtr ComInterfaceForObject(object instance)
        {
            if (s_globalInstanceForMarshalling == null)
            {
                throw new NotSupportedException(SR.InvalidOperation_ComInteropRequireComWrapperInstance);
            }

            return s_globalInstanceForMarshalling.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);
        }

        internal static unsafe IntPtr ComInterfaceForObject(object instance, Guid targetIID)
        {
            IntPtr unknownPtr = ComInterfaceForObject(instance);
            IntPtr comObjectInterface;
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)unknownPtr);
            int resultCode = wrapper->QueryInterface(in targetIID, out comObjectInterface);
            // We no longer need IUnknownPtr, release reference
            Marshal.Release(unknownPtr);
            if (resultCode != 0)
            {
                throw new InvalidCastException();
            }

            return comObjectInterface;
        }

        internal static object ComObjectForInterface(IntPtr externalComObject)
        {
            if (s_globalInstanceForMarshalling == null)
            {
                throw new NotSupportedException(SR.InvalidOperation_ComInteropRequireComWrapperInstance);
            }

            // TrackerObject support and unwrapping matches the built-in semantics that the global marshalling scenario mimics.
            return s_globalInstanceForMarshalling.GetOrCreateObjectForComInstance(externalComObject, CreateObjectFlags.TrackerObject | CreateObjectFlags.Unwrap);
        }

        internal static IntPtr GetOrCreateTrackerTarget(IntPtr externalComObject)
        {
            if (s_globalInstanceForTrackerSupport == null)
            {
                throw new NotSupportedException(SR.InvalidOperation_ComInteropRequireComWrapperTrackerInstance);
            }

            object obj = s_globalInstanceForTrackerSupport.GetOrCreateObjectForComInstance(externalComObject, CreateObjectFlags.TrackerObject);
            return s_globalInstanceForTrackerSupport.GetOrCreateComInterfaceForObject(obj, CreateComInterfaceFlags.TrackerSupport);
        }

        internal static void ReleaseExternalObjectsFromCurrentThread()
        {
            if (s_globalInstanceForTrackerSupport == null)
            {
                throw new NotSupportedException(SR.InvalidOperation_ComInteropRequireComWrapperTrackerInstance);
            }

            IntPtr contextToken = GetContextToken();

            List<object> objects = new List<object>();

            // Here we aren't part of a GC callback, so other threads can still be running
            // who are adding and removing from the collection. This means we can possibly race
            // with a handle being removed and freed and we can end up accessing a freed handle.
            // To avoid this, we take a lock on modifications to the collection while we gather
            // the objects.
            using (s_referenceTrackerNativeObjectWrapperCache.ModificationLock.EnterScope())
            {
                foreach (GCHandle weakNativeObjectWrapperHandle in s_referenceTrackerNativeObjectWrapperCache)
                {
                    ReferenceTrackerNativeObjectWrapper? nativeObjectWrapper = Unsafe.As<ReferenceTrackerNativeObjectWrapper?>(weakNativeObjectWrapperHandle.Target);
                    if (nativeObjectWrapper != null &&
                        nativeObjectWrapper._contextToken == contextToken)
                    {
                        object? target = nativeObjectWrapper.ProxyHandle.Target;
                        if (target != null)
                        {
                            objects.Add(target);
                        }

                        // Separate the wrapper from the tracker runtime prior to
                        // passing them.
                        nativeObjectWrapper.DisconnectTracker();
                    }
                }
            }

            s_globalInstanceForTrackerSupport.ReleaseObjects(objects);
        }

        // Used during GC callback
        internal static unsafe void WalkExternalTrackerObjects()
        {
            bool walkFailed = false;

            foreach (GCHandle weakNativeObjectWrapperHandle in s_referenceTrackerNativeObjectWrapperCache)
            {
                ReferenceTrackerNativeObjectWrapper? nativeObjectWrapper = Unsafe.As<ReferenceTrackerNativeObjectWrapper?>(weakNativeObjectWrapperHandle.Target);
                if (nativeObjectWrapper != null &&
                    nativeObjectWrapper.TrackerObject != IntPtr.Zero)
                {
                    FindReferenceTargetsCallback.s_currentRootObjectHandle = nativeObjectWrapper.ProxyHandle;
                    if (IReferenceTracker.FindTrackerTargets(nativeObjectWrapper.TrackerObject, TrackerObjectManager.s_findReferencesTargetCallback) != HResults.S_OK)
                    {
                        walkFailed = true;
                        FindReferenceTargetsCallback.s_currentRootObjectHandle = default;
                        break;
                    }
                    FindReferenceTargetsCallback.s_currentRootObjectHandle = default;
                }
            }

            // Report whether walking failed or not.
            if (walkFailed)
            {
                TrackerObjectManager.s_isGlobalPeggingOn = true;
            }
            IReferenceTrackerManager.FindTrackerTargetsCompleted(TrackerObjectManager.s_trackerManager, walkFailed);
        }

        // Used during GC callback
        internal static void DetachNonPromotedObjects()
        {
            foreach (GCHandle weakNativeObjectWrapperHandle in s_referenceTrackerNativeObjectWrapperCache)
            {
                ReferenceTrackerNativeObjectWrapper? nativeObjectWrapper = Unsafe.As<ReferenceTrackerNativeObjectWrapper?>(weakNativeObjectWrapperHandle.Target);
                if (nativeObjectWrapper != null &&
                    nativeObjectWrapper.TrackerObject != IntPtr.Zero &&
                    !RuntimeImports.RhIsPromoted(nativeObjectWrapper.ProxyHandle.Target))
                {
                    // Notify the wrapper it was not promoted and is being collected.
                    TrackerObjectManager.BeforeWrapperFinalized(nativeObjectWrapper.TrackerObject);
                }
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IUnknown_QueryInterface(IntPtr pThis, Guid* guid, IntPtr* ppObject)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->QueryInterface(in *guid, out *ppObject);
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IUnknown_Release(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            uint refcount = wrapper->Release();
            return refcount;
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerTarget_QueryInterface(IntPtr pThis, Guid* guid, IntPtr* ppObject)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->QueryInterfaceForTracker(in *guid, out *ppObject);
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IReferenceTrackerTarget_AddRefFromReferenceTracker(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->AddRefFromReferenceTracker();
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IReferenceTrackerTarget_ReleaseFromReferenceTracker(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->ReleaseFromReferenceTracker();
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IReferenceTrackerTarget_Peg(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->Peg();
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint IReferenceTrackerTarget_Unpeg(IntPtr pThis)
        {
            ManagedObjectWrapper* wrapper = ComInterfaceDispatch.ToManagedObjectWrapper((ComInterfaceDispatch*)pThis);
            return wrapper->Unpeg();
        }

        [UnmanagedCallersOnly]
        internal static unsafe int ITaggedImpl_IsCurrentVersion(IntPtr pThis, IntPtr version)
        {
            return version == (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&ITaggedImpl_IsCurrentVersion
                ? HResults.S_OK
                : HResults.E_FAIL;
        }

        private static unsafe IntPtr CreateDefaultIUnknownVftbl()
        {
            IntPtr* vftbl = (IntPtr*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComWrappers), 3 * sizeof(IntPtr));
            GetIUnknownImpl(out vftbl[0], out vftbl[1], out vftbl[2]);
            return (IntPtr)vftbl;
        }

        // This IID represents an internal interface we define to tag any ManagedObjectWrappers we create.
        // This interface type and GUID do not correspond to any public interface; it is an internal implementation detail.
        private static unsafe IntPtr CreateTaggedImplVftbl()
        {
            IntPtr* vftbl = (IntPtr*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComWrappers), 4 * sizeof(IntPtr));
            GetIUnknownImpl(out vftbl[0], out vftbl[1], out vftbl[2]);
            vftbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&ITaggedImpl_IsCurrentVersion;
            return (IntPtr)vftbl;
        }

        private static unsafe IntPtr CreateDefaultIReferenceTrackerTargetVftbl()
        {
            IntPtr* vftbl = (IntPtr*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComWrappers), 7 * sizeof(IntPtr));
            vftbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&ComWrappers.IReferenceTrackerTarget_QueryInterface;
            GetIUnknownImpl(out _, out vftbl[1], out vftbl[2]);
            vftbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IReferenceTrackerTarget_AddRefFromReferenceTracker;
            vftbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IReferenceTrackerTarget_ReleaseFromReferenceTracker;
            vftbl[5] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IReferenceTrackerTarget_Peg;
            vftbl[6] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.IReferenceTrackerTarget_Unpeg;
            return (IntPtr)vftbl;
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_DisconnectUnusedReferenceSources(IntPtr pThis, uint flags)
        {
            try
            {
                // Defined in windows.ui.xaml.hosting.referencetracker.h.
                const uint XAML_REFERENCETRACKER_DISCONNECT_SUSPEND = 0x00000001;

                if ((flags & XAML_REFERENCETRACKER_DISCONNECT_SUSPEND) != 0)
                {
                    RuntimeImports.RhCollect(2, InternalGCCollectionMode.Blocking | InternalGCCollectionMode.Optimized, true);
                }
                else
                {
                    GC.Collect();
                }
                return HResults.S_OK;
            }
            catch (Exception e)
            {
                return Marshal.GetHRForException(e);
            }

        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_ReleaseDisconnectedReferenceSources(IntPtr pThis)
        {
            // We'd like to call GC.WaitForPendingFinalizers() here, but this could lead to deadlock
            // if the finalizer thread is trying to get back to this thread, because we are not pumping
            // anymore. Disable this for now. See: https://github.com/dotnet/runtime/issues/109538.
            return HResults.S_OK;
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_NotifyEndOfReferenceTrackingOnThread(IntPtr pThis)
        {
            try
            {
                ReleaseExternalObjectsFromCurrentThread();
                return HResults.S_OK;
            }
            catch (Exception e)
            {
                return Marshal.GetHRForException(e);
            }

        }

        // Creates a proxy object (managed object wrapper) that points to the given IUnknown.
        // The proxy represents the following:
        //   1. Has a managed reference pointing to the external object
        //      and therefore forms a cycle that can be resolved by GC.
        //   2. Forwards data binding requests.
        //
        // For example:
        // NoCW = Native Object Com Wrapper also known as RCW
        //
        // Grid <---- NoCW             Grid <-------- NoCW
        // | ^                         |              ^
        // | |             Becomes     |              |
        // v |                         v              |
        // Rectangle                  Rectangle ----->Proxy
        //
        // Arguments
        //   obj        - An IUnknown* where a NoCW points to (Grid, in this case)
        //                    Notes:
        //                    1. We can either create a new NoCW or get back an old one from the cache.
        //                    2. This obj could be a regular tracker runtime object for data binding.
        //  ppNewReference  - The IReferenceTrackerTarget* for the proxy created
        //                    The tracker runtime will call IReferenceTrackerTarget to establish a reference.
        //
        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_GetTrackerTarget(IntPtr pThis, IntPtr punk, IntPtr* ppNewReference)
        {
            if (punk == IntPtr.Zero)
            {
                return HResults.E_INVALIDARG;
            }

            if (Marshal.QueryInterface(punk, IID_IUnknown, out IntPtr ppv) != HResults.S_OK)
            {
                return HResults.COR_E_INVALIDCAST;
            }

            try
            {
                using ComHolder identity = new ComHolder(ppv);
                using ComHolder trackerTarget = new ComHolder(GetOrCreateTrackerTarget(identity.Ptr));
                return Marshal.QueryInterface(trackerTarget.Ptr, IID_IReferenceTrackerTarget, out *ppNewReference);
            }
            catch (Exception e)
            {
                return Marshal.GetHRForException(e);
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_AddMemoryPressure(IntPtr pThis, long bytesAllocated)
        {
            try
            {
                GC.AddMemoryPressure(bytesAllocated);
                return HResults.S_OK;
            }
            catch (Exception e)
            {
                return Marshal.GetHRForException(e);
            }
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_RemoveMemoryPressure(IntPtr pThis, long bytesAllocated)
        {
            try
            {
                GC.RemoveMemoryPressure(bytesAllocated);
                return HResults.S_OK;
            }
            catch (Exception e)
            {
                return Marshal.GetHRForException(e);
            }
        }

        // Lifetime maintained by stack - we don't care about ref counts
        [UnmanagedCallersOnly]
        internal static unsafe uint Untracked_AddRef(IntPtr pThis)
        {
            return 1;
        }

        [UnmanagedCallersOnly]
        internal static unsafe uint Untracked_Release(IntPtr pThis)
        {
            return 1;
        }

        [UnmanagedCallersOnly]
        internal static unsafe int IReferenceTrackerHost_QueryInterface(IntPtr pThis, Guid* guid, IntPtr* ppObject)
        {
            if (*guid == IID_IReferenceTrackerHost || *guid == IID_IUnknown)
            {
                *ppObject = pThis;
                return 0;
            }
            else
            {
                return HResults.COR_E_INVALIDCAST;
            }
        }

        internal static unsafe IntPtr CreateDefaultIReferenceTrackerHostVftbl()
        {
            IntPtr* vftbl = (IntPtr*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(ComWrappers), 9 * sizeof(IntPtr));
            vftbl[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&ComWrappers.IReferenceTrackerHost_QueryInterface;
            vftbl[1] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.Untracked_AddRef;
            vftbl[2] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&ComWrappers.Untracked_Release;
            vftbl[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint, int>)&ComWrappers.IReferenceTrackerHost_DisconnectUnusedReferenceSources;
            vftbl[4] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ComWrappers.IReferenceTrackerHost_ReleaseDisconnectedReferenceSources;
            vftbl[5] = (IntPtr)(delegate* unmanaged<IntPtr, int>)&ComWrappers.IReferenceTrackerHost_NotifyEndOfReferenceTrackingOnThread;
            vftbl[6] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr*, int>)&ComWrappers.IReferenceTrackerHost_GetTrackerTarget;
            vftbl[7] = (IntPtr)(delegate* unmanaged<IntPtr, long, int>)&ComWrappers.IReferenceTrackerHost_AddMemoryPressure;
            vftbl[8] = (IntPtr)(delegate* unmanaged<IntPtr, long, int>)&ComWrappers.IReferenceTrackerHost_RemoveMemoryPressure;
            return (IntPtr)vftbl;
        }

        private static IntPtr GetContextToken()
        {
#if TARGET_WINDOWS
            Interop.Ole32.CoGetContextToken(out IntPtr contextToken);
            return contextToken;
#else
            return IntPtr.Zero;
#endif
        }

        // Wrapper for IWeakReference
        private static unsafe class IWeakReference
        {
            public static int Resolve(IntPtr pThis, Guid guid, out IntPtr inspectable)
            {
                fixed (IntPtr* inspectablePtr = &inspectable)
                    return (*(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>**)pThis)[3](pThis, &guid, inspectablePtr);
            }
        }

        // Wrapper for IWeakReferenceSource
        private static unsafe class IWeakReferenceSource
        {
            public static int GetWeakReference(IntPtr pThis, out IntPtr weakReference)
            {
                fixed (IntPtr* weakReferencePtr = &weakReference)
                    return (*(delegate* unmanaged<IntPtr, IntPtr*, int>**)pThis)[3](pThis, weakReferencePtr);
            }
        }

        private static object? ComWeakRefToObject(IntPtr pComWeakRef, long wrapperId)
        {
            if (wrapperId == 0)
            {
                return null;
            }

            // Using the IWeakReference*, get ahold of the target native COM object's IInspectable*.  If this resolve fails or
            // returns null, then we assume that the underlying native COM object is no longer alive, and thus we cannot create a
            // new RCW for it.
            if (IWeakReference.Resolve(pComWeakRef, IID_IInspectable, out IntPtr targetPtr) == HResults.S_OK &&
                targetPtr != IntPtr.Zero)
            {
                using ComHolder target = new ComHolder(targetPtr);
                if (Marshal.QueryInterface(target.Ptr, IID_IUnknown, out IntPtr targetIdentityPtr) == HResults.S_OK)
                {
                    using ComHolder targetIdentity = new ComHolder(targetIdentityPtr);
                    return GetOrCreateObjectFromWrapper(wrapperId, targetIdentity.Ptr);
                }
            }

            return null;
        }

        private static unsafe bool PossiblyComObject(object target)
        {
            // If the RCW is an aggregated RCW, then the managed object cannot be recreated from the IUnknown
            // as the outer IUnknown wraps the managed object. In this case, don't create a weak reference backed
            // by a COM weak reference.
            return s_nativeObjectWrapperTable.TryGetValue(target, out NativeObjectWrapper? wrapper) && !wrapper.IsAggregatedWithManagedObjectWrapper;
        }

        private static unsafe IntPtr ObjectToComWeakRef(object target, out long wrapperId)
        {
            if (TryGetComInstanceForIID(
                target,
                IID_IWeakReferenceSource,
                out IntPtr weakReferenceSourcePtr,
                out wrapperId))
            {
                using ComHolder weakReferenceSource = new ComHolder(weakReferenceSourcePtr);
                if (IWeakReferenceSource.GetWeakReference(weakReferenceSource.Ptr, out IntPtr weakReference) == HResults.S_OK)
                {
                    return weakReference;
                }
            }

            return IntPtr.Zero;
        }
    }

    // This is a GCHandle HashSet implementation based on LowLevelDictionary.
    // It uses no locking for readers. While for writers (add / remove),
    // it handles the locking itself.
    // This implementation specifically makes sure that any readers of this
    // collection during GC aren't impacted by other threads being
    // frozen while in the middle of an write. It makes no guarantees on
    // whether you will observe the element being added / removed, but does
    // make sure the collection is in a good state and doesn't run into issues
    // while iterating.
    internal sealed class GCHandleSet : IEnumerable<GCHandle>
    {
        private const int DefaultSize = 7;

        private Entry?[] _buckets = new Entry[DefaultSize];
        private int _numEntries;
        private readonly Lock _lock = new Lock(useTrivialWaits: true);

        public Lock ModificationLock => _lock;

        public void Add(GCHandle handle)
        {
            using (_lock.EnterScope())
            {
                int bucket = GetBucket(handle, _buckets.Length);
                Entry? prev = null;
                Entry? entry = _buckets[bucket];
                while (entry != null)
                {
                    // Handle already exists, nothing to add.
                    if (handle.Equals(entry.m_value))
                    {
                        return;
                    }

                    prev = entry;
                    entry = entry.m_next;
                }

                Entry newEntry = new Entry()
                {
                    m_value = handle
                };

                if (prev == null)
                {
                    _buckets[bucket] = newEntry;
                }
                else
                {
                    prev.m_next = newEntry;
                }

                // _numEntries is only maintained for the purposes of deciding whether to
                // expand the bucket and is not used during iteration to handle the
                // scenario where element is in bucket but _numEntries hasn't been incremented
                // yet.
                _numEntries++;
                if (_numEntries > (_buckets.Length * 2))
                {
                    ExpandBuckets();
                }
            }
        }

        private void ExpandBuckets()
        {
            int newNumBuckets = _buckets.Length * 2 + 1;
            Entry?[] newBuckets = new Entry[newNumBuckets];
            for (int i = 0; i < _buckets.Length; i++)
            {
                Entry? entry = _buckets[i];
                while (entry != null)
                {
                    Entry? nextEntry = entry.m_next;

                    int bucket = GetBucket(entry.m_value, newNumBuckets);

                    // We are allocating new entries for the bucket to ensure that
                    // if there is an enumeration already in progress, we don't
                    // modify what it observes by changing next in existing instances.
                    Entry newEntry = new Entry()
                    {
                        m_value = entry.m_value,
                        m_next = newBuckets[bucket],
                    };
                    newBuckets[bucket] = newEntry;

                    entry = nextEntry;
                }
            }
            _buckets = newBuckets;
        }

        public void Remove(GCHandle handle)
        {
            using (_lock.EnterScope())
            {
                int bucket = GetBucket(handle, _buckets.Length);
                Entry? prev = null;
                Entry? entry = _buckets[bucket];
                while (entry != null)
                {
                    if (handle.Equals(entry.m_value))
                    {
                        if (prev == null)
                        {
                            _buckets[bucket] = entry.m_next;
                        }
                        else
                        {
                            prev.m_next = entry.m_next;
                        }
                        _numEntries--;
                        return;
                    }

                    prev = entry;
                    entry = entry.m_next;
                }
            }
        }

        private static int GetBucket(GCHandle handle, int numBuckets)
        {
            int h = handle.GetHashCode();
            return (int)((uint)h % (uint)numBuckets);
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<GCHandle> IEnumerable<GCHandle>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<GCHandle>)this).GetEnumerator();

        private sealed class Entry
        {
            public GCHandle m_value;
            public Entry? m_next;
        }

        public struct Enumerator : IEnumerator<GCHandle>
        {
            private readonly Entry?[] _buckets;
            private int _currentIdx;
            private Entry? _currentEntry;

            public Enumerator(GCHandleSet set)
            {
                // We hold onto the buckets of the set rather than the set itself
                // so that if it is ever expanded, we are not impacted by that during
                // enumeration.
                _buckets = set._buckets;
                Reset();
            }

            public GCHandle Current
            {
                get
                {
                    if (_currentEntry == null)
                    {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return _currentEntry.m_value;
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_currentEntry != null)
                {
                    _currentEntry = _currentEntry.m_next;
                }

                if (_currentEntry == null)
                {
                    // Certain buckets might be empty, so loop until we find
                    // one with an entry.
                    while (++_currentIdx != _buckets.Length)
                    {
                        _currentEntry = _buckets[_currentIdx];
                        if (_currentEntry != null)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                return true;
            }

            public void Reset()
            {
                _currentIdx = -1;
                _currentEntry = null;
            }
        }
    }
}
