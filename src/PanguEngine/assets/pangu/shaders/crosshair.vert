#version 450

layout(location = 0) in vec2 inPosition;

layout(set = 0, binding = 0, std140) uniform CrosshairUniform
{
    mat4 projection;
} crosshair;

void main()
{
    gl_Position = crosshair.projection * vec4(inPosition, 0.0, 1.0);
}
