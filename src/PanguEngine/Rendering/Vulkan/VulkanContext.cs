using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace PanguEngine.Rendering.Vulkan;

/// <summary>
/// Manages Vulkan instance, device, and queue initialization lifecycle.
/// </summary>
public sealed unsafe class VulkanContext(bool enableValidationLayers = true)
{
    private readonly string[] _validationLayers = ["VK_LAYER_KHRONOS_validation"];
    private readonly string[] _deviceExtensions = [KhrSwapchain.ExtensionName];

    /// <summary>
    /// Whether Vulkan validation layers are enabled.
    /// </summary>
    public bool EnableValidationLayers { get; } = enableValidationLayers;

    /// <summary>
    /// The core Vulkan API.
    /// </summary>
    public Vk Vk { get; private set; } = null!;

    /// <summary>
    /// The Vulkan instance handle.
    /// </summary>
    public Instance VkInstance { get; private set; }
    
    private ExtDebugUtils? _debugUtils;
    private DebugUtilsMessengerEXT _debugMessenger;
    private DebugUtilsMessengerCallbackFunctionEXT? _debugCallbackDelegate;

    /// <summary>
    /// The VK_KHR_surface extension.
    /// </summary>
    public KhrSurface KhrSurface { get; private set; } = null!;

    /// <summary>
    /// The VK_KHR_swapchain extension.
    /// </summary>
    public KhrSwapchain KhrSwapchain { get; private set; } = null!;

    /// <summary>
    /// The selected physical device (GPU).
    /// </summary>
    public PhysicalDevice PhysicalDevice { get; private set; }

    /// <summary>
    /// The logical device handle.
    /// </summary>
    public Device Device { get; private set; }

    /// <summary>
    /// The queue family index for graphics operations.
    /// </summary>
    public uint GraphicsQueueFamily { get; private set; }

    /// <summary>
    /// The queue family index for presentation operations.
    /// </summary>
    public uint PresentQueueFamily { get; private set; }

    /// <summary>
    /// The graphics queue handle.
    /// </summary>
    public Queue GraphicsQueue { get; private set; }

    /// <summary>
    /// The presentation queue handle.
    /// </summary>
    public Queue PresentQueue { get; private set; }
    
    /// <summary>
    /// Maximum number of frames that can be processed concurrently.
    /// </summary>
    public int MaxFramesInFlight { get; private set; }

    private bool _instanceInitialized;
    private bool _deviceInitialized;
    private bool _destroyed;

    /// <summary>
    /// Initializes the Vulkan instance with the specified required extensions.
    /// </summary>
    /// <param name="requiredExtensions">Extensions required by the application (e.g., surface extensions).</param>
    internal void InitInstance(string[] requiredExtensions)
    {
        if (_instanceInitialized)
            throw new InvalidOperationException("Vulkan instance already initialized.");
        _instanceInitialized = true;

        Vk = Vk.GetApi();

        if (EnableValidationLayers && !CheckValidationLayerSupport())
            throw new InvalidOperationException("Validation layers requested, but not available.");

        CreateInstance(requiredExtensions);

        if (EnableValidationLayers)
            SetupDebugMessenger();

        if (!Vk.TryGetInstanceExtension<KhrSurface>(VkInstance, out var khrSurface))
            throw new NotSupportedException("VK_KHR_surface extension not found.");
        
        KhrSurface = khrSurface;
    }

    /// <summary>
    /// Initializes the logical device and queues for the given surface.
    /// </summary>
    /// <param name="surface">The window surface to present to.</param>
    internal void InitDevice(SurfaceKHR surface)
    {
        if (!_instanceInitialized)
            throw new InvalidOperationException("Vulkan instance must be initialized first.");
        if (_deviceInitialized)
            throw new InvalidOperationException("Logical device already initialized.");
        _deviceInitialized = true;

        PickPhysicalDevice(surface);

        KhrSurface.GetPhysicalDeviceSurfaceCapabilities(PhysicalDevice, surface, out var capabilities);
        MaxFramesInFlight = (int)(capabilities.MinImageCount + 1);
        if (capabilities.MaxImageCount > 0 && MaxFramesInFlight > capabilities.MaxImageCount)
            MaxFramesInFlight = (int)capabilities.MaxImageCount;

        CreateLogicalDevice(surface);
        
        if (!Vk.TryGetDeviceExtension<KhrSwapchain>(VkInstance, Device, out var khrSwapchain))
            throw new NotSupportedException("VK_KHR_swapchain extension not found.");
        KhrSwapchain = khrSwapchain;
    }

    /// <summary>
    /// Releases all Vulkan resources in the correct order.
    /// </summary>
    internal void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        if (_deviceInitialized)
        {
            Vk.DeviceWaitIdle(Device);
            Vk.DestroyDevice(Device, null);
        }

        if (EnableValidationLayers && _debugUtils is not null)
        {
            _debugUtils.DestroyDebugUtilsMessenger(VkInstance, _debugMessenger, null);
        }

        if (_instanceInitialized)
        {
            Vk.DestroyInstance(VkInstance, null);
            Vk.Dispose();
        }
    }

    private void CreateInstance(string[] requiredExtensions)
    {
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("PanguEngine"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("PanguEngine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        var extensions = EnableValidationLayers
            ? requiredExtensions.Append(ExtDebugUtils.ExtensionName).ToArray()
            : requiredExtensions;

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = (uint)extensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions)
        };

        DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)_validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_validationLayers);
            debugCreateInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
            debugCreateInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                               DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                               DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
            debugCreateInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                          DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
            _debugCallbackDelegate = DebugCallback;
            debugCreateInfo.PfnUserCallback = _debugCallbackDelegate;
            createInfo.PNext = &debugCreateInfo;
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
            createInfo.PNext = null;
        }

        if (Vk.CreateInstance(in createInfo, null, out var instance) != Result.Success)
            throw new InvalidOperationException("Failed to create Vulkan instance.");
        VkInstance = instance;

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }
    
    private void SetupDebugMessenger()
    {
        if (!Vk.TryGetInstanceExtension(VkInstance, out _debugUtils)) return;
        
        _debugCallbackDelegate = DebugCallback;
        DebugUtilsMessengerCreateInfoEXT createInfo = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                              DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                          DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                          DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = _debugCallbackDelegate
        };

        if (_debugUtils!.CreateDebugUtilsMessenger(VkInstance, in createInfo, null, out _debugMessenger) != Result.Success)
            throw new InvalidOperationException("Failed to set up debug messenger.");
    }

    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData)
    {
        var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
        Console.WriteLine($"Vulkan validation: {message}");
        return Vk.False;
    }

    private bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        Vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            Vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        var availableLayerNames = availableLayers
            .Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName))
            .ToHashSet();

        return _validationLayers.All(availableLayerNames.Contains);
    }

    private void PickPhysicalDevice(SurfaceKHR surface)
    {
        var devices = Vk.GetPhysicalDevices(VkInstance);

        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device, surface))
            {
                PhysicalDevice = device;
                break;
            }
        }

        if (PhysicalDevice.Handle == 0)
            throw new InvalidOperationException("Failed to find a suitable GPU.");
    }

    private bool IsDeviceSuitable(PhysicalDevice device, SurfaceKHR surface)
    {
        var indices = FindQueueFamilies(device, surface);
        var extensionsSupported = CheckDeviceExtensionsSupport(device);

        var swapChainAdequate = false;
        if (extensionsSupported)
        {
            var swapChainSupport = QuerySwapChainSupport(device, surface);
            swapChainAdequate = swapChainSupport.Formats.Length > 0 && swapChainSupport.PresentModes.Length > 0;
        }

        return indices.IsComplete && extensionsSupported && swapChainAdequate;
    }

    private bool CheckDeviceExtensionsSupport(PhysicalDevice device)
    {
        uint extensionsCount = 0;
        Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionsCount, null);

        var availableExtensions = new ExtensionProperties[extensionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
        {
            Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionsCount, availableExtensionsPtr);
        }

        var availableExtensionNames = availableExtensions
            .Select(ext => Marshal.PtrToStringAnsi((IntPtr)ext.ExtensionName))
            .ToHashSet();

        return _deviceExtensions.All(availableExtensionNames.Contains);
    }

    private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, SurfaceKHR surface)
    {
        var indices = new QueueFamilyIndices();

        uint queueFamilyCount = 0;
        Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
        }

        uint i = 0;
        foreach (var queueFamily in queueFamilies)
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                indices.GraphicsFamily = i;

            KhrSurface.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

            if (presentSupport)
                indices.PresentFamily = i;

            if (indices.IsComplete)
                break;

            i++;
        }

        return indices;
    }

    /// <summary>
    /// Queries swapchain support details for a physical device and surface.
    /// </summary>
    /// <param name="device">The physical device to query.</param>
    /// <param name="surface">The surface to query against.</param>
    /// <returns>Swapchain capabilities, formats, and present modes.</returns>
    internal SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device, SurfaceKHR surface)
    {
        var details = new SwapChainSupportDetails();

        KhrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out details.Capabilities);

        uint formatCount = 0;
        KhrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, null);

        if (formatCount != 0)
        {
            details.Formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
            {
                KhrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, formatsPtr);
            }
        }
        else
        {
            details.Formats = [];
        }

        uint presentModeCount = 0;
        KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.PresentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* presentModesPtr = details.PresentModes)
            {
                KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref presentModeCount, presentModesPtr);
            }
        }
        else
        {
            details.PresentModes = [];
        }

        return details;
    }

    private void CreateLogicalDevice(SurfaceKHR surface)
    {
        var indices = FindQueueFamilies(PhysicalDevice, surface);

        GraphicsQueueFamily = indices.GraphicsFamily!.Value;
        PresentQueueFamily = indices.PresentFamily!.Value;

        var uniqueQueueFamilies = new[] { GraphicsQueueFamily, PresentQueueFamily }
            .Distinct()
            .ToArray();

        using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

        var queuePriority = 1.0f;
        for (var i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        PhysicalDeviceFeatures deviceFeatures = new();

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)_deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(_deviceExtensions)
        };

        if (EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)_validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_validationLayers);
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
        }

        if (Vk.CreateDevice(PhysicalDevice, in createInfo, null, out var device) != Result.Success)
            throw new InvalidOperationException("Failed to create logical device.");
        Device = device;

        Vk.GetDeviceQueue(Device, GraphicsQueueFamily, 0, out var graphicsQueue);
        Vk.GetDeviceQueue(Device, PresentQueueFamily, 0, out var presentQueue);
        GraphicsQueue = graphicsQueue;
        PresentQueue = presentQueue;

        if (EnableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
    }

    /// <summary>
    /// Holds indices for graphics and presentation queue families.
    /// </summary>
    private struct QueueFamilyIndices
    {
        /// <summary>
        /// Index of the graphics queue family, or null if not found.
        /// </summary>
        public uint? GraphicsFamily;

        /// <summary>
        /// Index of the presentation queue family, or null if not found.
        /// </summary>
        public uint? PresentFamily;

        /// <summary>
        /// Whether both required queue families have been found.
        /// </summary>
        public bool IsComplete => GraphicsFamily.HasValue && PresentFamily.HasValue;
    }

    /// <summary>
    /// Contains swapchain support details for a physical device and surface.
    /// </summary>
    internal struct SwapChainSupportDetails
    {
        /// <summary>
        /// Surface capabilities (min/max image count, extents, etc.).
        /// </summary>
        public SurfaceCapabilitiesKHR Capabilities;

        /// <summary>
        /// Available surface formats.
        /// </summary>
        public SurfaceFormatKHR[] Formats;

        /// <summary>
        /// Available presentation modes.
        /// </summary>
        public PresentModeKHR[] PresentModes;
    }
}