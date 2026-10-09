#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 inUv;
layout(location = 3) in vec4 inClampBounds;
layout(location = 4) in uint inDrawMetadata;

layout(push_constant) uniform UiProjection
{
    vec2 clipScale;
} ui;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragUv;
layout(location = 2) flat out vec4 fragClampBounds;
layout(location = 3) flat out uint fragMaterialKind;
layout(location = 4) flat out uint fragTextureIndex;
layout(location = 5) flat out uint fragDrawDataIndex;

const uint TextureIndexShift = 2u;
const uint DrawDataIndexShift = 10u;
const uint MaterialKindMask = (1u << TextureIndexShift) - 1u;
const uint TextureIndexMask = (1u << (DrawDataIndexShift - TextureIndexShift)) - 1u;

void main()
{
    gl_Position = vec4(inPosition * ui.clipScale - 1.0, 0.0, 1.0);
    fragColor = inColor;
    fragUv = inUv;
    fragClampBounds = inClampBounds;
    fragMaterialKind = inDrawMetadata & MaterialKindMask;
    fragTextureIndex = (inDrawMetadata >> TextureIndexShift) & TextureIndexMask;
    fragDrawDataIndex = inDrawMetadata >> DrawDataIndexShift;
}
