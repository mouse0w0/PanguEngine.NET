#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec2 inTexCoord;
layout(location = 2) in vec3 inNormal;

layout(set = 0, binding = 0, std140) uniform WorldUniform
{
    mat4 viewProjection;
    vec4 lightDirection;
    vec4 lightColor;
    vec4 ambientColor;
} world;

layout(push_constant) uniform ObjectPushConstants
{
    vec4 translatedWorldPosition;
} objectData;

layout(location = 0) out vec2 fragTexCoord;
layout(location = 1) out vec3 fragNormal;

void main()
{
    vec3 translatedVertexPosition = inPosition + objectData.translatedWorldPosition.xyz;
    gl_Position = world.viewProjection * vec4(translatedVertexPosition, 1.0);
    fragTexCoord = inTexCoord;
    fragNormal = inNormal;
}
