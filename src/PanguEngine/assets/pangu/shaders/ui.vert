#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 inUv;
layout(location = 3) in vec4 inClampBounds;
layout(location = 4) in uint inMaterialData;

layout(push_constant) uniform UiProjection
{
    vec2 clipScale;
} ui;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragUv;
layout(location = 2) flat out vec4 fragClampBounds;
layout(location = 3) flat out uint fragMaterialKind;
layout(location = 4) flat out uint fragTextureIndex;

const uint TextureIndexShift = 8u;
const uint MaterialKindMask = (1u << TextureIndexShift) - 1u;

void main()
{
    gl_Position = vec4(inPosition * ui.clipScale - 1.0, 0.0, 1.0);
    fragColor = inColor;
    fragUv = inUv;
    fragClampBounds = inClampBounds;
    fragMaterialKind = inMaterialData & MaterialKindMask;
    fragTextureIndex = inMaterialData >> TextureIndexShift;
}
