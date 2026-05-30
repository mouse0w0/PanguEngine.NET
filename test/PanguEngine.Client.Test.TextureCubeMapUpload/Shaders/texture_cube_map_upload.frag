#version 450

layout(set = 0, binding = 0) uniform samplerCube textureSampler;

layout(location = 0) in vec2 fragTexCoord;
layout(location = 0) out vec4 outColor;

void main()
{
    int index = int(clamp(floor(fragTexCoord.x * 6.0), 0.0, 5.0));
    vec3 dirs[6] = vec3[6](
        vec3(1.0, 0.0, 0.0),
        vec3(-1.0, 0.0, 0.0),
        vec3(0.0, 1.0, 0.0),
        vec3(0.0, -1.0, 0.0),
        vec3(0.0, 0.0, 1.0),
        vec3(0.0, 0.0, -1.0));
    outColor = texture(textureSampler, dirs[index]);
}
