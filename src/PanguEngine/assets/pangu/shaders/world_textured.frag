#version 450

layout(set = 0, binding = 0, std140) uniform WorldUniform
{
    mat4 viewProjection;
    vec4 lightDirection;
    vec4 lightColor;
    vec4 ambientColor;
} world;

layout(set = 1, binding = 0) uniform sampler2D blockAtlas;

layout(location = 0) in vec2 fragTexCoord;
layout(location = 1) in vec3 fragNormal;
layout(location = 0) out vec4 outColor;

void main()
{
    vec4 albedo = texture(blockAtlas, fragTexCoord);
    float diffuse = max(dot(normalize(fragNormal), world.lightDirection.xyz), 0.0);
    vec3 lighting = clamp(world.ambientColor.rgb + diffuse * world.lightColor.rgb, 0.0, 1.0);
    outColor = vec4(albedo.rgb * lighting, albedo.a);
}
