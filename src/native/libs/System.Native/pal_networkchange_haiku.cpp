// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern "C"
{

#include "pal_errno.h"
#include "pal_networkchange.h"

}

#include <new>

#include <Looper.h>
#include <NetworkNotifications.h>

class NetworkChangeLooper : public BLooper
{
private:
    NetworkChangeEvent _handler = nullptr;
public:
    NetworkChangeLooper(const char* name)
        : BLooper(name)
    {
    }

    virtual void MessageReceived(BMessage* message) override
    {
        switch (message->what)
        {
            case B_NETWORK_MONITOR:
            {
                if (_handler != nullptr)
                {
                    int opcode = message->GetInt32("opcode", 0);
                    switch (opcode)
                    {
                        case B_NETWORK_INTERFACE_CHANGED:
                            // Raised by wide variety of events, including address added and removed.
                            _handler(reinterpret_cast<intptr_t>(this), AddressAdded);
                        break;
                        case B_NETWORK_INTERFACE_ADDED:
                        case B_NETWORK_INTERFACE_REMOVED:
                        case B_NETWORK_DEVICE_LINK_CHANGED:
                        case B_NETWORK_WLAN_JOINED:
                        case B_NETWORK_WLAN_LEFT:
                            _handler(reinterpret_cast<intptr_t>(this), AvailabilityChanged);
                        break;
                    }
                }
            }
            break;
            default:
                BLooper::MessageReceived(message);
            break;
        }
    }

    void SetHandler(NetworkChangeEvent handler)
    {
        _handler = handler;
    }

    NetworkChangeEvent GetHandler()
    {
        return _handler;
    }
};

extern "C"
{

// Despite the name, this function does not create a socket like on
// other UNIXes. Instead, it returns a handle to a NetworkChangeLooper.
Error SystemNative_CreateNetworkChangeListenerSocket(intptr_t* handle)
{
    auto looper = new (std::nothrow) NetworkChangeLooper("pal network change listener");
    if (looper == nullptr)
    {
        return Error_ENOMEM;
    }
    status_t status = start_watching_network(B_WATCH_NETWORK_INTERFACE_CHANGES
        | B_WATCH_NETWORK_LINK_CHANGES | B_WATCH_NETWORK_WLAN_CHANGES, looper);
    if (status != B_OK)
    {
        delete looper;
        return static_cast<Error>(SystemNative_ConvertErrorPlatformToPal(status));
    }
    *handle = reinterpret_cast<intptr_t>(looper);
    return Error_SUCCESS;
}

// Instead of actually reading the events, this function initializes the
// NetworkChangeLooper with a handler and starts watching on a new thread.
Error SystemNative_ReadEvents(intptr_t handle, NetworkChangeEvent onNetworkChange)
{
    auto looper = reinterpret_cast<NetworkChangeLooper*>(handle);
    looper->SetHandler(onNetworkChange);
    if (looper->Thread() == B_ERROR)
    {
        status_t status = looper->Run();
        if (status < 0)
        {
            return static_cast<Error>(SystemNative_ConvertErrorPlatformToPal(status));
        }
    }
    return Error_SUCCESS;
}

Error SystemNative_DestroyNetworkChangeListener(intptr_t handle)
{
    auto looper = reinterpret_cast<NetworkChangeLooper*>(handle);
    status_t status = stop_watching_network(looper);
    if (status != B_OK)
    {
        return static_cast<Error>(SystemNative_ConvertErrorPlatformToPal(status));
    }
    status = looper->PostMessage(B_QUIT_REQUESTED);
    // If this operation succeeds, the looper will delete itself.
    return static_cast<Error>(SystemNative_ConvertErrorPlatformToPal(status));
}

}
