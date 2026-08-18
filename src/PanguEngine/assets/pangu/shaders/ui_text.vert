#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 inUv;
layout(location = 3) in vec4 inClampBounds;

layout(push_constant) uniform UiProjection
{
    vec2 clipScale;
} ui;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragUv;
layout(location = 2) out vec4 fragClampBounds;

void main()
{
    gl_Position = vec4(
        inPosition.x * ui.clipScale.x - 1.0,
        inPosition.y * ui.clipScale.y - 1.0,
        0.0,
        1.0);
    fragColor = inColor;
    fragUv = inUv;
    fragClampBounds = inClampBounds;
}
