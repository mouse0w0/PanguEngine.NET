#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;

layout(push_constant) uniform UiProjection
{
    vec2 clipScale;
} ui;

layout(location = 0) out vec4 fragColor;

void main()
{
    gl_Position = vec4(
        inPosition.x * ui.clipScale.x - 1.0,
        inPosition.y * ui.clipScale.y - 1.0,
        0.0,
        1.0);
    fragColor = inColor;
}
