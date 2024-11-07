#version 450 core

in vec3 v_position;
in vec3 v_world_position;
in vec3 v_normal;

uniform vec3 u_camera_position;

uniform vec4 u_color;
uniform vec4 bg_color;

uniform float absorption_coef;
uniform float steps;
uniform mat4 u_model;

out vec4 FragColor;


vec3 mod289(vec3 x)
{
  return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 mod289(vec4 x)
{
  return x - floor(x * (1.0 / 289.0)) * 289.0;
}

vec4 permute(vec4 x)
{
  return mod289(((x*34.0)+1.0)*x);
}

vec4 taylorInvSqrt(vec4 r)
{
  return 1.79284291400159 - 0.85373472095314 * r;
}

vec3 fade(vec3 t) {
  return t*t*t*(t*(t*6.0-15.0)+10.0);
}

// Classic Perlin noise
float cnoise(vec3 P)
{
  vec3 Pi0 = floor(P); // Integer part for indexing
  vec3 Pi1 = Pi0 + vec3(1.0); // Integer part + 1
  Pi0 = mod289(Pi0);
  Pi1 = mod289(Pi1);
  vec3 Pf0 = fract(P); // Fractional part for interpolation
  vec3 Pf1 = Pf0 - vec3(1.0); // Fractional part - 1.0
  vec4 ix = vec4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
  vec4 iy = vec4(Pi0.yy, Pi1.yy);
  vec4 iz0 = Pi0.zzzz;
  vec4 iz1 = Pi1.zzzz;

  vec4 ixy = permute(permute(ix) + iy);
  vec4 ixy0 = permute(ixy + iz0);
  vec4 ixy1 = permute(ixy + iz1);

  vec4 gx0 = ixy0 * (1.0 / 7.0);
  vec4 gy0 = fract(floor(gx0) * (1.0 / 7.0)) - 0.5;
  gx0 = fract(gx0);
  vec4 gz0 = vec4(0.5) - abs(gx0) - abs(gy0);
  vec4 sz0 = step(gz0, vec4(0.0));
  gx0 -= sz0 * (step(0.0, gx0) - 0.5);
  gy0 -= sz0 * (step(0.0, gy0) - 0.5);

  vec4 gx1 = ixy1 * (1.0 / 7.0);
  vec4 gy1 = fract(floor(gx1) * (1.0 / 7.0)) - 0.5;
  gx1 = fract(gx1);
  vec4 gz1 = vec4(0.5) - abs(gx1) - abs(gy1);
  vec4 sz1 = step(gz1, vec4(0.0));
  gx1 -= sz1 * (step(0.0, gx1) - 0.5);
  gy1 -= sz1 * (step(0.0, gy1) - 0.5);

  vec3 g000 = vec3(gx0.x,gy0.x,gz0.x);
  vec3 g100 = vec3(gx0.y,gy0.y,gz0.y);
  vec3 g010 = vec3(gx0.z,gy0.z,gz0.z);
  vec3 g110 = vec3(gx0.w,gy0.w,gz0.w);
  vec3 g001 = vec3(gx1.x,gy1.x,gz1.x);
  vec3 g101 = vec3(gx1.y,gy1.y,gz1.y);
  vec3 g011 = vec3(gx1.z,gy1.z,gz1.z);
  vec3 g111 = vec3(gx1.w,gy1.w,gz1.w);

  vec4 norm0 = taylorInvSqrt(vec4(dot(g000, g000), dot(g010, g010), dot(g100, g100), dot(g110, g110)));
  g000 *= norm0.x;
  g010 *= norm0.y;
  g100 *= norm0.z;
  g110 *= norm0.w;
  vec4 norm1 = taylorInvSqrt(vec4(dot(g001, g001), dot(g011, g011), dot(g101, g101), dot(g111, g111)));
  g001 *= norm1.x;
  g011 *= norm1.y;
  g101 *= norm1.z;
  g111 *= norm1.w;

  float n000 = dot(g000, Pf0);
  float n100 = dot(g100, vec3(Pf1.x, Pf0.yz));
  float n010 = dot(g010, vec3(Pf0.x, Pf1.y, Pf0.z));
  float n110 = dot(g110, vec3(Pf1.xy, Pf0.z));
  float n001 = dot(g001, vec3(Pf0.xy, Pf1.z));
  float n101 = dot(g101, vec3(Pf1.x, Pf0.y, Pf1.z));
  float n011 = dot(g011, vec3(Pf0.x, Pf1.yz));
  float n111 = dot(g111, Pf1);

  vec3 fade_xyz = fade(Pf0);
  vec4 n_z = mix(vec4(n000, n100, n010, n110), vec4(n001, n101, n011, n111), fade_xyz.z);
  vec2 n_yz = mix(n_z.xy, n_z.zw, fade_xyz.y);
  float n_xyz = mix(n_yz.x, n_yz.y, fade_xyz.x);
  return 2.2 * n_xyz;
}

float turbulence(vec3 p) {
    float w = 100.0;
    float t = 0.0;
    for (float f = 1.0; f <= 10.0; f *= 2.0) {
        t += abs(cnoise(p * f)) / f;
    }
    return t;
}

float fbm(vec3 p) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 8; i++) {  // Using more octaves (e.g., 8) for smoother clouds
        value += amplitude * cnoise(p);
        p *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

float cloudDensity(float value) {
    return smoothstep(0.1, 0.2, value);  // Adjust thresholds for density range
}

float combinedNoise(vec3 p) {
    float fBmNoise = fbm(p * 0.5);            // Lower frequency for large cloud shapes
    float turbulenceNoise = turbulence(p * 1.5);  // Higher frequency for fine details
    return (fBmNoise * 0.7) + (turbulenceNoise * 0.3); // Weighted combination
}

float adjustContrast(float val, float contrast) {
    return pow(val, contrast); // Adjust for denser regions
}

float color(vec3 xyz) {
    float n = combinedNoise(xyz);
    float density = cloudDensity(n);
    return adjustContrast(density, 1.05) * 0.5;  // Use a slight contrast adjustment (e.g., 1.3)
}

void main()
{
  vec3 rayOrigin = u_camera_position;
	vec3 rayDir = normalize(v_world_position - rayOrigin);
	
	vec3 localBoxMin = vec3(-1.0, -1.0, -1.0);
	vec3 localBoxMax = vec3(1.0, 1.0, 1.0);
	vec3 boxMin = (u_model * vec4(localBoxMin, 1.0)).xyz;
	vec3 boxMax = (u_model * vec4(localBoxMax, 1.0)).xyz;

	vec3 tMin = (boxMin - rayOrigin) / rayDir;
	vec3 tMax = (boxMax - rayOrigin) / rayDir;
	vec3 t1 = min(tMin, tMax);
	vec3 t2 = max(tMin, tMax);
	float ta = max(max(t1.x, t1.y), t1.z);
	float tb = min(min(t2.x, t2.y), t2.z);

	vec4 radiance = vec4(0.0f);
	float count = 0.0;
	float step_ta = ta;
	float step_tb = tb;
	float noiseValue;
	float n;

	for (float i = ta; i<tb; i += steps) {
		count++;
		vec3 p = v_world_position + i * rayDir;
		n = color(p);
    
		radiance += bg_color * exp(-(tb - ta) * n * absorption_coef * 0.5);
	}
	radiance = radiance / count;

	FragColor = radiance;
}