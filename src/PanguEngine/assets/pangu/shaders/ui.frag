#version 450
#extension GL_EXT_nonuniform_qualifier : require

layout(set = 0, binding = 0) uniform texture2D uiTextures[256];
layout(set = 0, binding = 1) uniform sampler linearSampler;
layout(set = 0, binding = 2) uniform sampler nearestSampler;

struct UiDrawData
{
    vec4 background;
    vec4 border;
    uvec4 indices;
};

struct UiClipData
{
    vec4 bounds;
    vec4 radiusX;
    vec4 radiusY;
    uvec4 links;
};

layout(std430, set = 1, binding = 0) readonly buffer UiDrawBuffer
{
    UiDrawData drawData[];
};

layout(std430, set = 1, binding = 1) readonly buffer UiClipBuffer
{
    UiClipData clipData[];
};

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragUv;
layout(location = 2) flat in vec4 fragClampBounds;
layout(location = 3) flat in uint fragMaterialKind;
layout(location = 4) flat in uint fragTextureIndex;
layout(location = 5) flat in uint fragDrawDataIndex;
layout(location = 0) out vec4 outColor;

float cornerBoundary(vec2 position, vec2 radius)
{
    if (radius.x <= 0.0 || radius.y <= 0.0 || any(greaterThanEqual(position, radius)))
        return -1e20;
    vec2 q = (radius - position) / radius;
    float k = length(q);
    float gradientSpan = dot(q / radius, vec2(1.0));
    return (k - 1.0) * k / max(gradientSpan, 1e-20);
}

float roundedBoundary(UiClipData shape, vec2 position)
{
    vec2 first = position - shape.bounds.xy;
    vec2 last = shape.bounds.zw - position;
    vec2 axisCoverage = clamp(min(last + 0.5, vec2(1.0))
        - max(0.5 - first, vec2(0.0)), vec2(0.0), vec2(1.0));
    float boundary = 0.5 - axisCoverage.x * axisCoverage.y;
    if (boundary >= 0.5)
        return boundary;
    boundary = max(boundary, cornerBoundary(first, vec2(shape.radiusX.x, shape.radiusY.x)));
    boundary = max(boundary, cornerBoundary(vec2(last.x, first.y), vec2(shape.radiusX.y, shape.radiusY.y)));
    boundary = max(boundary, cornerBoundary(last, vec2(shape.radiusX.z, shape.radiusY.z)));
    return max(boundary, cornerBoundary(vec2(first.x, last.y), vec2(shape.radiusX.w, shape.radiusY.w)));
}

float clipBoundary(uint index, vec2 position)
{
    float boundary = -1e20;
    while (index != 0u)
    {
        UiClipData clip = clipData[index];
        float value = roundedBoundary(clip, position);
        if (clip.links.y != 0u)
            value = -value;
        boundary = max(boundary, value);
        if (boundary >= 0.5)
            return boundary;
        index = clip.links.x;
    }
    return boundary;
}

float boundaryCoverage(float boundary)
{
    return clamp(0.5 - boundary, 0.0, 1.0);
}

vec2 ellipsePoint(vec2 radius, float angle)
{
    return radius * vec2(cos(angle), sin(angle));
}

float ellipseDistanceDerivative(vec2 position, vec2 radius, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return (radius.y * radius.y - radius.x * radius.x) * s * c
        + radius.x * position.x * s - radius.y * position.y * c;
}

vec2 closestEllipseArc(vec2 position, vec2 radius)
{
    vec2 first = vec2(radius.x, 0.0);
    vec2 last = vec2(0.0, radius.y);
    vec2 closest = dot(position - first, position - first) < dot(position - last, position - last)
        ? first : last;
    float low = 0.0;
    float high = 1.5707963267948966;
    float delta = radius.y * radius.y - radius.x * radius.x;
    if (position.x >= 0.0 && position.y >= 0.0)
    {
        if (position.x == 0.0 && delta <= radius.y * position.y)
            return closest;
        if (position.y == 0.0 && -delta <= radius.x * position.x)
            return closest;
        if (radius.x == radius.y)
            return radius.x * normalize(position);
    }
    else if (position.x < 0.0 && position.y > 0.0)
    {
        high = atan(pow(-radius.y * position.y / (radius.x * position.x), 1.0 / 3.0));
        if (ellipseDistanceDerivative(position, radius, high) <= 0.0)
            return closest;
    }
    else if (position.x > 0.0 && position.y < 0.0)
    {
        low = atan(pow(-radius.y * position.y / (radius.x * position.x), 1.0 / 3.0));
        if (ellipseDistanceDerivative(position, radius, low) >= 0.0)
            return closest;
    }
    else
        return closest;
    for (int i = 0; i < 24; ++i)
    {
        float middle = (low + high) * 0.5;
        if (ellipseDistanceDerivative(position, radius, middle) < 0.0)
            low = middle;
        else
            high = middle;
        if ((high - low) * max(radius.x, radius.y) < 0.001)
            break;
    }
    vec2 candidate = ellipsePoint(radius, (low + high) * 0.5);
    return dot(position - candidate, position - candidate) < dot(position - closest, position - closest)
        ? candidate : closest;
}

float roundedStrokeCoverage(UiClipData centerline, vec2 position, float halfWidth, float clip)
{
    vec2 halfSize = (centerline.bounds.zw - centerline.bounds.xy) * 0.5;
    vec2 point = abs(position - (centerline.bounds.xy + halfSize));
    vec2 radius = vec2(centerline.radiusX.x, centerline.radiusY.x);
    vec2 straight = halfSize - radius;
    vec2 right = vec2(halfSize.x, clamp(point.y, 0.0, straight.y));
    vec2 bottom = vec2(clamp(point.x, 0.0, straight.x), halfSize.y);
    vec2 closest = dot(point - right, point - right) < dot(point - bottom, point - bottom) ? right : bottom;
    vec2 cornerPosition = point - straight;
    vec2 arcBounds = clamp(point, straight, halfSize);
    if (dot(point - arcBounds, point - arcBounds) < dot(point - closest, point - closest))
    {
        vec2 arc = straight + closestEllipseArc(cornerPosition, radius);
        if (dot(point - arc, point - arc) < dot(point - closest, point - closest))
            closest = arc;
    }
    vec2 difference = point - closest;
    float distance = length(difference);
    float span = max(dot(abs(difference) / max(distance, 1e-20), vec2(1.0)), 1.0);
    bool inside = all(lessThanEqual(point, halfSize))
        && (any(lessThanEqual(cornerPosition, vec2(0.0))) || length(cornerPosition / radius) <= 1.0);
    float signedDistance = inside ? -distance : distance;
    float outer = max(clip, (signedDistance - halfWidth) / span);
    if (halfWidth >= min(halfSize.x, halfSize.y))
        return boundaryCoverage(outer);
    float inner = max(clip, (signedDistance + halfWidth) / span);
    return max(boundaryCoverage(outer) - boundaryCoverage(inner), 0.0);
}

float rectangleStrokeCoverage(UiClipData centerline, vec2 position, float halfWidth, float clip, uint mode)
{
    UiClipData outer = centerline;
    outer.bounds += vec4(-halfWidth, -halfWidth, halfWidth, halfWidth);
    outer.radiusX = vec4(mode == 5u ? halfWidth : 0.0);
    outer.radiusY = outer.radiusX;
    float boundary = roundedBoundary(outer, position);
    if (mode == 4u)
    {
        vec2 halfSize = (centerline.bounds.zw - centerline.bounds.xy) * 0.5;
        vec2 corner = abs(position - (centerline.bounds.xy + halfSize)) - halfSize;
        boundary = max(boundary, (corner.x + corner.y - halfWidth) * 0.5);
    }
    float outerCoverage = boundaryCoverage(max(clip, boundary));
    UiClipData inner = centerline;
    inner.bounds += vec4(halfWidth, halfWidth, -halfWidth, -halfWidth);
    if (any(lessThanEqual(inner.bounds.zw, inner.bounds.xy)))
        return outerCoverage;
    float innerCoverage = boundaryCoverage(max(clip, roundedBoundary(inner, position)));
    return max(outerCoverage - innerCoverage, 0.0);
}

vec4 materialColor()
{
    if (fragMaterialKind == 0u)
        return fragColor;
    vec2 uv = clamp(fragUv, fragClampBounds.xy, fragClampBounds.zw);
    if (fragMaterialKind == 1u)
    {
        vec4 sampled = texture(sampler2D(
            uiTextures[nonuniformEXT(fragTextureIndex)], nearestSampler), uv);
        return vec4(sampled.rgb, sampled.a * fragColor.a);
    }
    if (fragMaterialKind == 2u)
    {
        vec4 sampled = texture(sampler2D(
            uiTextures[nonuniformEXT(fragTextureIndex)], linearSampler), uv);
        return vec4(sampled.rgb, sampled.a * fragColor.a);
    }
    float coverage = texture(sampler2D(
        uiTextures[nonuniformEXT(fragTextureIndex)], linearSampler), uv).r;
    return vec4(fragColor.rgb, fragColor.a * coverage);
}

void main()
{
    if (fragDrawDataIndex == 0u)
    {
        outColor = materialColor();
        return;
    }
    UiDrawData draw = drawData[fragDrawDataIndex];
    float boundary = clipBoundary(draw.indices.x, gl_FragCoord.xy);
    if (draw.indices.w != 0u && boundary >= 0.5)
    {
        outColor = vec4(0.0);
        return;
    }
    if (draw.indices.w >= 2u)
    {
        UiClipData centerline = clipData[draw.indices.y];
        float halfWidth = uintBitsToFloat(draw.indices.z);
        float strokeCoverage = 0.0;
        if (draw.border.a > 0.0)
        {
            if (draw.indices.w == 2u)
                strokeCoverage = roundedStrokeCoverage(centerline, gl_FragCoord.xy, halfWidth, boundary);
            else
                strokeCoverage = rectangleStrokeCoverage(centerline, gl_FragCoord.xy, halfWidth, boundary, draw.indices.w);
        }
        float fillCoverage = 0.0;
        if (draw.background.a > 0.0)
            fillCoverage = boundaryCoverage(max(boundary, roundedBoundary(centerline, gl_FragCoord.xy)));
        float strokeAlpha = draw.border.a * strokeCoverage;
        float fillAlpha = draw.background.a * fillCoverage * (1.0 - strokeAlpha);
        float alpha = strokeAlpha + fillAlpha;
        vec3 premultiplied = draw.border.rgb * strokeAlpha + draw.background.rgb * fillAlpha;
        outColor = alpha > 0.0 ? vec4(premultiplied / alpha, alpha) : vec4(0.0);
        return;
    }
    if (draw.indices.w == 1u)
    {
        UiClipData outer = clipData[draw.indices.y];
        boundary = max(boundary, roundedBoundary(outer, gl_FragCoord.xy));
        float outerCoverage = boundaryCoverage(boundary);
        float backgroundCoverage = 0.0;
        if (draw.indices.z != 0u)
        {
            UiClipData inner = clipData[draw.indices.z];
            backgroundCoverage = boundaryCoverage(
                max(boundary, roundedBoundary(inner, gl_FragCoord.xy)));
        }
        float backgroundAlpha = draw.background.a * backgroundCoverage;
        float borderAlpha = draw.border.a * max(outerCoverage - backgroundCoverage, 0.0);
        float alpha = backgroundAlpha + borderAlpha;
        vec3 premultiplied = draw.background.rgb * backgroundAlpha + draw.border.rgb * borderAlpha;
        outColor = alpha > 0.0 ? vec4(premultiplied / alpha, alpha) : vec4(0.0);
        return;
    }
    vec4 color = materialColor();
    outColor = vec4(color.rgb, color.a * boundaryCoverage(boundary));
}
