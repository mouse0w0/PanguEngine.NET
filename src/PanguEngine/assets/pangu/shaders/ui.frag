#version 450
#extension GL_EXT_nonuniform_qualifier : require

layout(set = 0, binding = 0) uniform texture2D uiTextures[256];
layout(set = 0, binding = 1) uniform sampler linearSampler;
layout(set = 0, binding = 2) uniform sampler nearestSampler;

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragUv;
layout(location = 2) flat in vec4 fragClampBounds;
layout(location = 3) flat in uint fragMaterialKind;
layout(location = 4) flat in uint fragTextureIndex;
layout(location = 0) out vec4 outColor;

void main()
{
    if (fragMaterialKind == 0u)
    {
        outColor = fragColor;
        return;
    }

    vec2 uv = clamp(fragUv, fragClampBounds.xy, fragClampBounds.zw);
    if (fragMaterialKind == 1u)
    {
        vec4 sampled = texture(sampler2D(
            uiTextures[nonuniformEXT(fragTextureIndex)], nearestSampler), uv);
        outColor = vec4(sampled.rgb, sampled.a * fragColor.a);
        return;
    }

    if (fragMaterialKind == 2u)
    {
        vec4 sampled = texture(sampler2D(
            uiTextures[nonuniformEXT(fragTextureIndex)], linearSampler), uv);
        outColor = vec4(sampled.rgb, sampled.a * fragColor.a);
        return;
    }

    float coverage = texture(sampler2D(
        uiTextures[nonuniformEXT(fragTextureIndex)], linearSampler), uv).r;
    outColor = vec4(fragColor.rgb, fragColor.a * coverage);
}
