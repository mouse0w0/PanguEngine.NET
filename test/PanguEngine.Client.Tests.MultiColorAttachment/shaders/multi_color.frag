#version 450

layout(location = 0) in vec3 fragColor;

layout(location = 0) out vec4 outScreenColor;
layout(location = 1) out vec4 outOffscreenColor;

void main()
{
    outScreenColor = vec4(fragColor, 1.0);
    outOffscreenColor = vec4(1.0 - fragColor, 1.0);
}
