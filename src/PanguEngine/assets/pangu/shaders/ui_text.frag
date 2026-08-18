#version 450

layout(set = 0, binding = 0) uniform sampler2D glyphAtlas;

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragUv;
layout(location = 2) in vec4 fragClampBounds;
layout(location = 0) out vec4 outColor;

void main()
{
    vec2 uv = clamp(fragUv, fragClampBounds.xy, fragClampBounds.zw);
    float coverage = texture(glyphAtlas, uv).r;
    outColor = vec4(fragColor.rgb, fragColor.a * coverage);
}
