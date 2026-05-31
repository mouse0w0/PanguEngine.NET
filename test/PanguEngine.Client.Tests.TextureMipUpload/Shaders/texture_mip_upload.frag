#version 450

layout(set = 0, binding = 0) uniform sampler2D textureSampler;

layout(location = 0) in vec2 fragTexCoord;
layout(location = 0) out vec4 outColor;

void main()
{
    float lod = fragTexCoord.x < 0.33333334 ? 0.0 : (fragTexCoord.x < 0.6666667 ? 1.0 : 2.0);
    outColor = textureLod(textureSampler, fragTexCoord, lod);
}
