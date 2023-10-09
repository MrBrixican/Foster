#ifndef FOSTER_INTERNAL_H
#define FOSTER_INTERNAL_H

#include "foster_platform.h"
#include "foster_renderer.h"
#include "foster_audio.h"
#include <SDL.h>
#include "third_party/miniaudio.h"

// foster global state
typedef struct
{
	bool running;
	FosterDesc desc;
	FosterFlags flags;
	FosterRenderDevice device;
	int windowCreateFlags;
	SDL_Window* window;
	SDL_Joystick* joysticks[FOSTER_MAX_CONTROLLERS];
	SDL_GameController* gamepads[FOSTER_MAX_CONTROLLERS];
	char* clipboardText;
	char* userPath;
	ma_engine* audioEngine;
} FosterState;

FosterState* FosterGetState();

void FosterLogInfo(const char* fmt, ...);

void FosterLogWarn(const char* fmt, ...);

void FosterLogError(const char* fmt, ...);

#endif