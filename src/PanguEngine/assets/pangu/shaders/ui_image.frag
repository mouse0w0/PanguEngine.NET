#version 450

layout(set = 0, binding = 0) uniform sampler2D uiImage;

layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec4 fragClampBounds;
layout(location = 2) in float fragOpacity;
layout(location = 0) out vec4 outColor;

void main()
{
    vec2 rawMin = fragClampBounds.xy;
    vec2 rawMax = fragClampBounds.zw;
    vec2 sampleMin = min(rawMin, rawMax);
    vec2 sampleMax = max(rawMin, rawMax);
    vec2 sampleUv = clamp(fragUv, sampleMin, sampleMax);
    if (rawMin.x > rawMax.x)
        sampleUv.x = (rawMin.x + rawMax.x) * 0.5;
    if (rawMin.y > rawMax.y)
        sampleUv.y = (rawMin.y + rawMax.y) * 0.5;

    vec4 sampled = texture(uiImage, sampleUv);
    outColor = vec4(sampled.rgb, sampled.a * fragOpacity);
}
