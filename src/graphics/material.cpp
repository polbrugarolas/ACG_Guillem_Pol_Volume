#include "material.h"

#include "application.h"

#include <istream>
#include <fstream>
#include <algorithm>


FlatMaterial::FlatMaterial(glm::vec4 color)
{
	this->color = color;
	this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/flat.fs");
}

FlatMaterial::~FlatMaterial() { }

void FlatMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	//upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);

	this->shader->setUniform("u_color", this->color);
}

void FlatMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (mesh && this->shader) {
		// enable shader
		this->shader->enable();

		// upload uniforms
		setUniforms(camera, model);

		// do the draw call
		mesh->render(GL_TRIANGLES);

		this->shader->disable();
	}
}

void FlatMaterial::renderInMenu()
{
	ImGui::ColorEdit3("Color", (float*)&this->color);
}

WireframeMaterial::WireframeMaterial()
{
	this->color = glm::vec4(1.f);
	this->shader = Shader::Get("res/shaders/basic.vs", "res/shaders/flat.fs");
}

WireframeMaterial::~WireframeMaterial() { }

void WireframeMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (this->shader && mesh)
	{
		glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
		glDisable(GL_CULL_FACE);

		//enable shader
		this->shader->enable();

		//upload material specific uniforms
		setUniforms(camera, model);

		//do the draw call
		mesh->render(GL_TRIANGLES);

		glEnable(GL_CULL_FACE);
		glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
	}
}

StandardMaterial::StandardMaterial(glm::vec4 color)
{
	this->color = color;
	this->base_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/basic.fs");
	this->normal_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/normal.fs");
	this->shader = this->base_shader;
}

StandardMaterial::~StandardMaterial() { }

void StandardMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	//upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);

	this->shader->setUniform("u_color", this->color);

	if (this->texture) {
		this->shader->setUniform("u_texture", this->texture);
	}
}

void StandardMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	bool first_pass = true;
	if (mesh && this->shader)
	{
		// enable shader
		this->shader->enable();

		// Multi pass render
		int num_lights = Application::instance->light_list.size();
		for (int nlight = -1; nlight < num_lights; nlight++)
		{
			if (nlight == -1) { nlight++; } // hotfix

			// upload uniforms
			setUniforms(camera, model);

			// upload light uniforms
			if (!first_pass) {
				glBlendFunc(GL_SRC_ALPHA, GL_ONE);
				glDepthFunc(GL_LEQUAL);
			}
			this->shader->setUniform("u_ambient_light", Application::instance->ambient_light * (float)first_pass);

			if (num_lights > 0) {
				Light* light = Application::instance->light_list[nlight];
				light->setUniforms(this->shader, model);
			}
			else {
				// Set some uniforms in case there is no light
				this->shader->setUniform("u_light_intensity", 1.f);
				this->shader->setUniform("u_light_shininess", 1.f);
				this->shader->setUniform("u_light_color", glm::vec4(0.f));
			}

			// do the draw call
			mesh->render(GL_TRIANGLES);

			first_pass = false;
		}

		// disable shader
		this->shader->disable();
	}
}

void StandardMaterial::renderInMenu()
{
	if (ImGui::Checkbox("Show Normals", &this->show_normals)) {
		if (this->show_normals) {
			this->shader = this->normal_shader;
		}
		else {
			this->shader = this->base_shader;
		}
	}

	if (!this->show_normals) ImGui::ColorEdit3("Color", (float*)&this->color);
}



VolumeMaterial::VolumeMaterial(glm::vec4 color)
{
	this->absorption_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/volume.fs");
	this->emission_shader = Shader::Get("res/shaders/basic.vs", "res/shaders/emission_volume.fs");
	this->absortion_coef = 0.5;
	this->step = 0.004;
	this->renderType = 0;
	this->shader = absorption_shader;
	this->color = glm::vec4(0.0, 0.0, 0.0, 1.0);
	this->scale = 2.209;
	this->detail = 5;
}

VolumeMaterial::~VolumeMaterial() { }

void VolumeMaterial::setUniforms(Camera* camera, glm::mat4 model)
{
	//upload node uniforms
	this->shader->setUniform("u_viewprojection", camera->viewprojection_matrix);
	this->shader->setUniform("u_camera_position", camera->eye);
	this->shader->setUniform("u_model", model);

	this->shader->setUniform("u_color", this->color);
	this->shader->setUniform("bg_color", bg_color);
	this->shader->setUniform("absorption_coef", this->absortion_coef);
	this->shader->setUniform("steps", this->step);
	this->shader->setUniform("renderType", this->renderType);
	this->shader->setUniform("scale", this->scale);
	this->shader->setUniform("detail", this->detail);
}

void VolumeMaterial::render(Mesh* mesh, glm::mat4 model, Camera* camera)
{
	if (mesh && this->shader) {
		// enable shader
		this->shader->enable();

		// upload uniforms
		setUniforms(camera, model);

		// do the draw call
		mesh->render(GL_TRIANGLES);

		this->shader->disable();
	}
}

int currentRenderMode = 0;
int currentVolumeType = 0;

void VolumeMaterial::renderInMenu()
{
	const char* volumeTypes[] = { "Absorption", "Emission-Absorption" };
	ImGui::Combo("Shader Type", &currentVolumeType, volumeTypes, IM_ARRAYSIZE(volumeTypes));
	
	if (currentVolumeType == 0) {
		this->shader = absorption_shader;
	}
	else {
		this->shader = emission_shader;
		ImGui::ColorEdit3("Color", (float*)&this->color);
	}

	ImGui::SliderFloat("Step Length", &this->step, 0.001, 1.0);
	ImGui::SliderFloat("Absortion Coefficient", &this->absortion_coef, 0.0, 2.0);

	const char* renderModes[] = { "Homogeneous", "Heterogeneous" };
	ImGui::Combo("Volume Type", &currentRenderMode, renderModes, IM_ARRAYSIZE(renderModes));

	if (currentRenderMode == 0) {
		this->renderType = 0;
	}
	else {
		this->renderType = 1;
		ImGui::SliderFloat("Noise Scale", &this->scale, 0.01, 5.0);
		ImGui::SliderFloat("Noise Detail", &this->detail, 0.01, 6.0);
	}
}