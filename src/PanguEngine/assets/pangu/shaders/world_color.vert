#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec4 inColor;

layout(location = 0) out vec4 fragColor;

void main()
{
    vec2 ndc = vec2(
        (inPosition.x - inPosition.z) * 0.06,
        -0.88 + (inPosition.x + inPosition.z) * 0.035 + inPosition.y * 0.08);
    gl_Position = vec4(ndc, 0.0, 1.0);
    fragColor = inColor;
}
