using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace PanguEngine.Graphics.Vulkan;

/// <summary>
/// Manages Vulkan instance, device, and queue initialization lifecycle.
/// </summary>
public static unsafe class VulkanContext
{
    private static readonly string[] ValidationLayers = ["VK_LAYER_KHRONOS_validation"];
    private static readonly string[] DeviceExtensions = [KhrSwapchain.ExtensionName];

    private static bool _enableValidationLayers;

    /// <summary>
    /// The core Vulkan API.
    /// </summary>
    public static Vk Vk { get; private set; } = null!;

    /// <summary>
    /// The Vulkan instance handle.
    /// </summary>
    public static Instance VkInstance { get; private set; }

    private static ExtDebugUtils? _debugUtils;
    private static DebugUtilsMessengerEXT _debugMessenger;
    private static DebugUtilsMessengerCallbackFunctionEXT? _debugCallbackDelegate;

    /// <summary>
    /// The VK_KHR_surface extension.
    /// </summary>
    public static KhrSurface KhrSurface { get; private set; } = null!;

    /// <summary>
    /// The VK_KHR_swapchain extension.
    /// </summary>
    public static KhrSwapchain KhrSwapchain { get; private set; } = null!;

    /// <summary>
    /// The selected physical device (GPU).
    /// </summary>
    public static PhysicalDevice PhysicalDevice { get; private set; }

    /// <summary>
    /// The logical device handle.
    /// </summary>
    public static Device Device { get; private set; }

    /// <summary>
    /// The queue family index for graphics operations.
    /// </summary>
    public static uint GraphicsQueueFamily { get; private set; }

    /// <summary>
    /// The queue family index for presentation operations.
    /// </summary>
    public static uint PresentQueueFamily { get; private set; }

    /// <summary>
    /// The graphics queue handle.
    /// </summary>
    public static Queue GraphicsQueue { get; private set; }

    /// <summary>
    /// The presentation queue handle.
    /// </summary>
    public static Queue PresentQueue { get; private set; }

    /// <summary>
    /// Maximum number of frames that can be processed concurrently.
    /// </summary>
    public static uint MaxFramesInFlight { get; private set; }

    /// <summary>
    /// Minimum required alignment for uniform buffer offsets, in bytes.
    /// </summary>
    public static ulong MinUniformBufferOffsetAlignment { get; private set; }

    /// <summary>
    /// Maximum supported width for one-dimensional images.
    /// </summary>
    public static uint MaxImageDimension1D { get; private set; }

    /// <summary>
    /// Maximum supported width or height for two-dimensional images.
    /// </summary>
    public static uint MaxImageDimension2D { get; private set; }

    /// <summary>
    /// Maximum supported width, height, or depth for three-dimensional images.
    /// </summary>
    public static uint MaxImageDimension3D { get; private set; }

    /// <summary>
    /// Maximum supported number of layers for array images.
    /// </summary>
    public static uint MaxImageArrayLayers { get; private set; }

    /// <summary>
    /// Gets whether sampler anisotropy is supported by the physical device.
    /// </summary>
    public static bool SamplerAnisotropySupported { get; private set; }

    /// <summary>
    /// Gets the maximum supported sampler anisotropy level.
    /// </summary>
    public static float MaxSamplerAnisotropy { get; private set; }

    /// <summary>
    /// Gets the maximum absolute sampler LOD bias supported by the physical device.
    /// </summary>
    public static float MaxSamplerLodBias { get; private set; }

    private static Semaphore _globalTimelineSemaphore;
    private static ulong _globalTimelineValue;

    /// <summary>
    /// A global timeline semaphore signaled by every window's QueueSubmit to track GPU progress across all windows.
    /// </summary>
    public static Semaphore GlobalTimelineSemaphore => _globalTimelineSemaphore;

    /// <summary>
    /// The latest CPU-side value of the global timeline counter.
    /// </summary>
    public static ulong GlobalTimelineValue => Volatile.Read(ref _globalTimelineValue);

    /// <summary>
    /// Atomically advances the global timeline counter and returns the value to signal in QueueSubmit.
    /// </summary>
    public static ulong NextGlobalTimelineValue()
    {
        return Interlocked.Increment(ref _globalTimelineValue);
    }

    private static bool _instanceInitialized;
    private static bool _deviceInitialized;
    private static bool _destroyed;

    /// <summary>
    /// Gets whether the Vulkan instance has been initialized.
    /// </summary>
    public static bool IsInstanceInitialized => _instanceInitialized;

    /// <summary>
    /// Initializes the Vulkan instance with the specified required extensions.
    /// </summary>
    /// <param name="requiredExtensions">Extensions required by the application (e.g., surface extensions).</param>
    /// <param name="enableValidationLayers">Whether to enable Vulkan validation layers for debugging.</param>
    internal static void InitializeInstance(string[] requiredExtensions, bool enableValidationLayers = true)
    {
        if (_instanceInitialized)
            throw new InvalidOperationException("Vulkan instance already initialized.");

        _enableValidationLayers = enableValidationLayers;
        Vk = Vk.GetApi();

        try
        {
            if (_enableValidationLayers && !CheckValidationLayerSupport())
                throw new InvalidOperationException("Validation layers requested, but not available.");

            CreateInstance(requiredExtensions);

            if (_enableValidationLayers)
                SetupDebugMessenger();

            if (!Vk.TryGetInstanceExtension<KhrSurface>(VkInstance, out var khrSurface))
                throw new NotSupportedException("VK_KHR_surface extension not found.");

            KhrSurface = khrSurface;
            _instanceInitialized = true;
        }
        catch
        {
            if (_enableValidationLayers && _debugUtils is not null && _debugMessenger.Handle != 0)
                _debugUtils.DestroyDebugUtilsMessenger(VkInstance, _debugMessenger, null);
            if (VkInstance.Handle != 0)
                Vk.DestroyInstance(VkInstance, null);

            Vk.Dispose();
            Vk = null!;
            VkInstance = default;
            _debugUtils = null;
            _debugMessenger = default;
            KhrSurface = null!;
            _instanceInitialized = false;

            throw;
        }
    }

    /// <summary>
    /// Initializes the Vulkan instance if it has not been initialized yet.
    /// </summary>
    /// <param name="requiredExtensions">Extensions required by the application.</param>
    /// <param name="enableValidationLayers">Whether to enable Vulkan validation layers for debugging.</param>
    internal static void EnsureInstanceInitialized(string[] requiredExtensions, bool enableValidationLayers = true)
    {
        if (IsInstanceInitialized) return;
        InitializeInstance(requiredExtensions, enableValidationLayers);
    }

    /// <summary>
    /// Initializes the logical device and queues for the given surface.
    /// </summary>
    /// <param name="surface">The window surface to present to.</param>
    internal static void InitializeDevice(SurfaceKHR surface)
    {
        if (!_instanceInitialized)
            throw new InvalidOperationException("Vulkan instance must be initialized first.");
        if (_deviceInitialized)
            throw new InvalidOperationException("Logical device already initialized.");

        try
        {
            PickPhysicalDevice(surface);

            Vk.GetPhysicalDeviceProperties(PhysicalDevice, out var props);
            Vk.GetPhysicalDeviceFeatures(PhysicalDevice, out var physicalDeviceFeatures);
            MinUniformBufferOffsetAlignment = props.Limits.MinUniformBufferOffsetAlignment;
            MaxImageDimension1D = props.Limits.MaxImageDimension1D;
            MaxImageDimension2D = props.Limits.MaxImageDimension2D;
            MaxImageDimension3D = props.Limits.MaxImageDimension3D;
            MaxImageArrayLayers = props.Limits.MaxImageArrayLayers;
            SamplerAnisotropySupported = physicalDeviceFeatures.SamplerAnisotropy;
            MaxSamplerAnisotropy = props.Limits.MaxSamplerAnisotropy;
            MaxSamplerLodBias = props.Limits.MaxSamplerLodBias;

            KhrSurface.GetPhysicalDeviceSurfaceCapabilities(PhysicalDevice, surface, out var capabilities);
            MaxFramesInFlight = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && MaxFramesInFlight > capabilities.MaxImageCount)
                MaxFramesInFlight = capabilities.MaxImageCount;

            CreateLogicalDevice(surface);

            SemaphoreTypeCreateInfo timelineCreateInfo = new()
            {
                SType = StructureType.SemaphoreTypeCreateInfo,
                SemaphoreType = SemaphoreType.Timeline,
                InitialValue = 0,
            };
            SemaphoreCreateInfo semaphoreInfo = new()
            {
                SType = StructureType.SemaphoreCreateInfo,
                PNext = &timelineCreateInfo,
            };
            if (Vk.CreateSemaphore(Device, in semaphoreInfo, null, out _globalTimelineSemaphore) != Result.Success)
                throw new InvalidOperationException("Failed to create global timeline semaphore.");

            if (!Vk.TryGetDeviceExtension<KhrSwapchain>(VkInstance, Device, out var khrSwapchain))
                throw new NotSupportedException("VK_KHR_swapchain extension not found.");

            KhrSwapchain = khrSwapchain;
            _deviceInitialized = true;
        }
        catch
        {
            if (_globalTimelineSemaphore.Handle != 0)
                Vk.DestroySemaphore(Device, _globalTimelineSemaphore, null);
            if (Device.Handle != 0)
                Vk.DestroyDevice(Device, null);

            _globalTimelineSemaphore = default;
            KhrSwapchain = null!;
            Device = default;
            GraphicsQueue = default;
            PresentQueue = default;
            PhysicalDevice = default;
            GraphicsQueueFamily = 0;
            PresentQueueFamily = 0;
            MaxFramesInFlight = 0;
            MinUniformBufferOffsetAlignment = 0;
            MaxImageDimension1D = 0;
            MaxImageDimension2D = 0;
            MaxImageDimension3D = 0;
            MaxImageArrayLayers = 0;
            SamplerAnisotropySupported = false;
            MaxSamplerAnisotropy = 0;
            MaxSamplerLodBias = 0;
            _deviceInitialized = false;

            throw;
        }
    }

    /// <summary>
    /// Releases all Vulkan resources in the correct order.
    /// </summary>
    internal static void Destroy()
    {
        if (_destroyed) return;
        _destroyed = true;

        if (_deviceInitialized)
        {
            Vk.DeviceWaitIdle(Device);
            Vk.DestroySemaphore(Device, _globalTimelineSemaphore, null);
            Vk.DestroyDevice(Device, null);
        }

        if (_enableValidationLayers && _debugUtils is not null)
        {
            _debugUtils.DestroyDebugUtilsMessenger(VkInstance, _debugMessenger, null);
        }

        if (_instanceInitialized)
        {
            Vk.DestroyInstance(VkInstance, null);
            Vk.Dispose();
        }
    }

    private static void CreateInstance(string[] requiredExtensions)
    {
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("PanguEngine"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("PanguEngine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13
        };

        var extensions = _enableValidationLayers
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
        if (_enableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
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

        if (_enableValidationLayers)
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
    }

    private static void SetupDebugMessenger()
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

        if (_debugUtils!.CreateDebugUtilsMessenger(VkInstance, in createInfo, null, out _debugMessenger) !=
            Result.Success)
            throw new InvalidOperationException("Failed to set up debug messenger.");
    }

    private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData)
    {
        var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
        Console.WriteLine($"Vulkan validation: {message}");
        return Vk.False;
    }

    private static bool CheckValidationLayerSupport()
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

        return ValidationLayers.All(availableLayerNames.Contains);
    }

    private static void PickPhysicalDevice(SurfaceKHR surface)
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

    private static bool IsDeviceSuitable(PhysicalDevice device, SurfaceKHR surface)
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

    private static bool CheckDeviceExtensionsSupport(PhysicalDevice device)
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

        return DeviceExtensions.All(availableExtensionNames.Contains);
    }

    private static QueueFamilyIndices FindQueueFamilies(PhysicalDevice device, SurfaceKHR surface)
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
    internal static SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device, SurfaceKHR surface)
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

    private static void CreateLogicalDevice(SurfaceKHR surface)
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

        PhysicalDeviceFeatures deviceFeatures = new()
        {
            SamplerAnisotropy = SamplerAnisotropySupported,
        };

        PhysicalDeviceVulkan12Features vulkan12Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan12Features,
            TimelineSemaphore = true,
        };

        PhysicalDeviceVulkan13Features vulkan13Features = new()
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            PNext = &vulkan12Features,
            DynamicRendering = true,
            Synchronization2 = true,
        };

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &vulkan13Features,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)DeviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(DeviceExtensions)
        };

        if (_enableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(ValidationLayers);
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

        if (_enableValidationLayers)
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