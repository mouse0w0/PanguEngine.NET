using PanguEngine.Graphics;
using GraphicsBuffer = PanguEngine.Graphics.Buffer;

namespace PanguEngine.Tests.UI;

internal sealed class GraphicsTestDevice : GraphicsDevice
{
    internal List<TestTexture> Textures { get; } = [];
    internal List<TestTextureView> TextureViews { get; } = [];
    internal List<TestSampler> Samplers { get; } = [];
    internal List<TestDescriptorSet> DescriptorSets { get; } = [];
    internal List<(Texture Texture, byte[] Data, TextureUploadRegion? Region)> Uploads { get; } = [];
    internal Exception? UploadException { get; set; }
    internal UploadHandle UploadHandle { get; set; } = TestUploadHandle.Succeeded();
    internal Exception? DescriptorUpdateException { get; set; }

    public override uint MaxTextureDimension2D => 4096;
    public override uint MaxDrawIndirectCount => 1;

    public override GraphicsBuffer CreateBuffer(in BufferDescription description) =>
        throw new NotSupportedException();

    public override UploadHandle UploadBuffer<T>(
        GraphicsBuffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0) => throw new NotSupportedException();

    public override Texture CreateTexture(in TextureDescription description)
    {
        var texture = new TestTexture(description);
        Textures.Add(texture);
        return texture;
    }

    public override TextureView CreateTextureView(
        Texture texture,
        in TextureViewDescription description)
    {
        var view = new TestTextureView(texture, description);
        TextureViews.Add(view);
        return view;
    }

    public override UploadHandle UploadTexture(Texture destination, ReadOnlySpan<byte> data)
    {
        if (UploadException is not null)
            throw UploadException;
        Uploads.Add((destination, data.ToArray(), null));
        return UploadHandle;
    }

    public override UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data,
        in TextureUploadRegion region)
    {
        if (UploadException is not null)
            throw UploadException;
        Uploads.Add((destination, data.ToArray(), region));
        return UploadHandle;
    }

    public override UploadHandle GenerateMipmaps(Texture texture) => throw new NotSupportedException();

    public override Sampler CreateSampler(in SamplerDescription description)
    {
        var sampler = new TestSampler(description);
        Samplers.Add(sampler);
        return sampler;
    }

    public override Shader CreateShader(in ShaderDescription description) => throw new NotSupportedException();

    public override DescriptorSetLayout CreateDescriptorSetLayout(
        in DescriptorSetLayoutDescription description) => new TestDescriptorSetLayout(description);

    public override DescriptorSet CreateDescriptorSet(in DescriptorSetDescription description)
    {
        var descriptorSet = new TestDescriptorSet(description, this);
        DescriptorSets.Add(descriptorSet);
        return descriptorSet;
    }

    public override ulong GetAlignedUniformSize(ulong rawSize) => rawSize;

    public override GraphicsPipeline CreateGraphicsPipeline(
        in GraphicsPipelineDescription description) => throw new NotSupportedException();

    public override void WaitIdle()
    {
    }

    internal TestTextureView CreateSampledView(uint width = 1, uint height = 1)
    {
        var texture = CreateTexture(new TextureDescription
        {
            Dimension = TextureDimension.Type2D,
            Format = TextureFormat.R8G8B8A8Srgb,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            Usage = TextureUsage.Sampled | TextureUsage.TransferDestination
        });
        return (TestTextureView)CreateTextureView(
            texture,
            new TextureViewDescription(TextureViewDimension.Type2D, 0, 1, 0, 1));
    }
}

internal sealed class TestTexture(TextureDescription description) : Texture
{
    public override TextureDimension Dimension => description.Dimension;
    public override TextureFormat Format => description.Format;
    public override uint Width => description.Width;
    public override uint Height => description.Height;
    public override uint Depth => description.Depth;
    public override uint MipLevels => description.MipLevels;
    public override uint ArrayLayers => description.ArrayLayers;
    public override TextureUsage Usage => description.Usage;
    public override TextureCreateFlags CreateFlags => description.Flags;

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestTextureView(
    Texture texture,
    TextureViewDescription description) : TextureView
{
    internal Exception? DestroyException { get; set; }

    public override Texture Texture => texture;
    public override TextureViewDimension Dimension => description.Dimension;
    public override TextureFormat Format => texture.Format;
    public override uint Width => texture.Width;
    public override uint Height => texture.Height;
    public override uint Depth => texture.Depth;
    public override uint BaseMipLevel => description.BaseMipLevel;
    public override uint MipLevels => description.MipLevels;
    public override uint BaseArrayLayer => description.BaseArrayLayer;
    public override uint ArrayLayers => description.ArrayLayers;

    public override void Destroy()
    {
        if (DestroyException is not null)
            throw DestroyException;
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestSampler(SamplerDescription description) : Sampler
{
    internal SamplerDescription Description => description;

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestDescriptorSetLayout(DescriptorSetLayoutDescription description) : DescriptorSetLayout
{
    internal DescriptorSetLayoutDescription Description => description;

    public override void Destroy()
    {
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestDescriptorSet(
    DescriptorSetDescription description,
    GraphicsTestDevice device) : DescriptorSet
{
    private readonly Dictionary<(uint Binding, uint ArrayElement), DescriptorSetBinding> _bindings =
        description.Bindings.ToDictionary(binding => (binding.Binding, binding.ArrayElement));

    internal DescriptorSetDescription Description => description;
    internal Exception? DestroyException { get; set; }
    internal List<DescriptorSetBinding[]> Updates { get; } = [];
    internal IReadOnlyDictionary<(uint Binding, uint ArrayElement), DescriptorSetBinding> Bindings => _bindings;

    public override void Update(DescriptorSetBinding[] bindings)
    {
        if (device.DescriptorUpdateException is not null)
            throw device.DescriptorUpdateException;
        Updates.Add([.. bindings]);
        foreach (var binding in bindings)
            _bindings[(binding.Binding, binding.ArrayElement)] = binding;
    }

    public override void Destroy()
    {
        if (DestroyException is not null)
            throw DestroyException;
        if (!IsDestroyed)
            MarkDestroyed();
    }
}

internal sealed class TestUploadHandle : UploadHandle
{
    private UploadState _state;
    private Exception? _exception;

    protected override UploadState State => _state;
    public override Exception? Exception => _exception;

    internal static TestUploadHandle Ready()
    {
        var handle = new TestUploadHandle();
        handle._state = UploadState.Ready;
        return handle;
    }

    internal static TestUploadHandle Succeeded()
    {
        var handle = new TestUploadHandle();
        handle._state = UploadState.Succeeded;
        return handle;
    }

    internal void SetReady() => _state = UploadState.Ready;
    internal void SetSucceeded() => _state = UploadState.Succeeded;

    internal void SetFaulted(Exception exception)
    {
        _exception = exception;
        _state = UploadState.Faulted;
    }
}
