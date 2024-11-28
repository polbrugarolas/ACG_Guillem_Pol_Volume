#version 450 core

in vec3 v_position;
in vec3 v_world_position;
in vec3 v_normal;

uniform vec3 u_camera_position;

uniform vec4 u_color;
uniform vec4 bg_color;
uniform int renderType;
uniform sampler3D u_texture;

uniform float absorption_coef;
uniform float steps;
uniform mat4 u_model;
uniform float scale;
uniform float detail;
uniform vec3 u_boxMin;
uniform vec3 u_boxMax;

// Instance light information
uniform float u_light_intensity;
uniform float u_light_shininess;
uniform vec4 u_light_color;
uniform vec3 u_light_direction;
uniform vec3 u_light_position;
uniform vec3 u_local_light_position;
uniform float scattering_coef;
uniform float g;

out vec4 FragColor;


// Noise functions
float hash1( float n )
{
    return fract( n*17.0*fract( n*0.3183099 ) );
}

float noise( vec3 x )
{
    vec3 p = floor(x);
    vec3 w = fract(x);
    
    vec3 u = w*w*w*(w*(w*6.0-15.0)+10.0);
    
    float n = p.x + 317.0*p.y + 157.0*p.z;
    
    float a = hash1(n+0.0);
    float b = hash1(n+1.0);
    float c = hash1(n+317.0);
    float d = hash1(n+318.0);
    float e = hash1(n+157.0);
    float f = hash1(n+158.0);
    float g = hash1(n+474.0);
    float h = hash1(n+475.0);

    float k0 =   a;
    float k1 =   b - a;
    float k2 =   c - a;
    float k3 =   e - a;
    float k4 =   a - b - c + d;
    float k5 =   a - c - e + g;
    float k6 =   a - b - e + f;
    float k7 = - a + b + c - d + e - f - g + h;

    return -1.0+2.0*(k0 + k1*u.x + k2*u.y + k3*u.z + k4*u.x*u.y + k5*u.y*u.z + k6*u.z*u.x + k7*u.x*u.y*u.z);
}

#define MAX_OCTAVES 16

float fractal_noise( vec3 P, float detail )
{
    float fscale = 1.0;
    float amp = 1.0;
    float sum = 0.0;
    float octaves = clamp(detail, 0.0, 16.0);
    int n = int(octaves);

    for (int i = 0; i <= MAX_OCTAVES; i++) {
        if (i > n) continue;
        float t = noise(fscale * P);
        sum += t * amp;
        amp *= 0.5;
        fscale *= 2.0;
    }

    return sum;
}

float cnoise( vec3 P, float scale, float detail )
{
    P *= scale;
    return clamp(fractal_noise(P, detail), 0.0, 1.0);
}

float rand(vec2 st) {
    return fract(sin(dot(st.xy, vec2(12.9898,78.233))) * 43758.5453123);
}





void main()
{
  // Generate a ray with the direction from the camera to the position
	vec3 rayOrigin = u_camera_position;
	vec3 rayDir = normalize(v_position - rayOrigin);
	
  vec3 boxMin = vec3(-1.0, -1.0, -1.0);
	vec3 boxMax = vec3(1.0, 1.0, 1.0);

	vec3 tMin = (u_boxMin - rayOrigin) / rayDir;
	vec3 tMax = (u_boxMax - rayOrigin) / rayDir;
	vec3 t1 = min(tMin, tMax);
	vec3 t2 = max(tMin, tMax);
	float ta = max(max(t1.x, t1.y), t1.z);
	float tb = min(min(t2.x, t2.y), t2.z);

	vec4 radiance = vec4(0.0f);
	float count = 0.0;
	float noiseValue;
	float n;
	float abs_coef_t = 0.0;
  float optical_thicknessTt = 0.0;
  float abs_coef_a = 0.0;
  float abs_coef_s = 0.0;
  vec4 BT = vec4(0.0f);
  vec3 p = u_camera_position;
  vec3 pt = u_camera_position;
  vec3 ptr = u_camera_position;
  vec4 Lst = vec4(0.0f);
  float scat_coef = 0.0;

  float phase = 1 / (4 * 3.14159265);
  float addition = rand(gl_FragCoord.xy);

  // Iterate through the ray
  for (float i = ta; i<tb; i += steps) {
    count++;
    p = rayOrigin + i * rayDir + rayDir  * steps * addition;

    // Generate a second ray from the position to the light source position
    vec3 rayLight = normalize(u_local_light_position - p);
    tMin = (u_boxMin - p) / rayLight;
    tMax = (u_boxMax - p) / rayLight;
    t2 = max(tMin, tMax);
    float tr = min(min(t2.x, t2.y), t2.z);
    vec3 pos = p + rayLight * tr;
    float steps2 = tr / 5;
    float count2 = 0.0;
    float optical_thickness_l = 0.0;

    // Iterate through this second ray
    for (float j = 0.0; j<tr; j += steps2) {
      count2++;
      pos = p + j * rayLight;

      // Compute Density
      if (renderType == 0) {
        // Constant Density
        n = 1.0;
      }
      else if (renderType == 1) {
        // Random Density
        n = cnoise(p, scale, detail);
      }
      else {
        // Bunny Density
        pt = vec3((p.x + 1) / 2, (p.y + 1) / 2, (p.z + 1) / 2);
        n = texture(u_texture, pt).x;
      }

      // Compute absorption coefficient and optical thicknes off the light ray
      float abs_coef_l = n * absorption_coef;
      optical_thickness_l += abs_coef_l * steps2;
    }

    // Compute transmittance and in-scattered color
    float Tl = exp(-optical_thickness_l);
    Lst = u_light_color * u_light_intensity * u_light_shininess * Tl;

    float phase_HG = phase;

    // Comment from line 177 to 180 to use isotropic scattering
    float angle = acos(dot(rayDir, rayLight));
    float upper = phase * (1 - g * g);
    float down = pow((1 + g * g - 2 * g * cos(angle)), 3/2);
    phase_HG = upper / down;

    // Add phase function to control directivity of the scattered light
    Lst = Lst * phase_HG;

    // Compute Density
    if (renderType == 0) {
      // Constant Density
      n = 1.0;
	  }
	  else if (renderType == 1) {
			// Random Density
			n = cnoise(p, scale, detail);
		}
    else {
			// Bunny Density
			pt = vec3((p.x + 1) / 2, (p.y + 1) / 2, (p.z + 1) / 2);
      n = texture(u_texture, pt).x;
		}

    // Compute absorption, scattering and extinction coefficients
    abs_coef_a = n * absorption_coef;
    scat_coef = n * scattering_coef;
    abs_coef_t = abs_coef_a + scat_coef;

    // Compute Optical Thickness
    optical_thicknessTt += abs_coef_t * steps;

    // Compute radiance: Emitted Light + Out-Scattering Light
    radiance += exp(-optical_thicknessTt) * (u_color * abs_coef_t + scat_coef * Lst) * steps;
	}

  // Compute Background color transmittion
  BT = bg_color * exp(-optical_thicknessTt);

  // Add Background color to the final color
  radiance += BT;

  // Return final color
	FragColor = radiance;
}