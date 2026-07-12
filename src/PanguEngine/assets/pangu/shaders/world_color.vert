#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec4 inColor;

layout(set = 0, binding = 0, std140) uniform CameraUniform
{
    mat4 viewProjection;
} camera;

layout(location = 0) out vec4 fragColor;

void main()
{
    gl_Position = camera.viewProjection * vec4(inPosition, 1.0);
    fragColor = inColor;
}
