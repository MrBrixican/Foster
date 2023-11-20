using Foster.Framework.Audio;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Foster.Framework;

internal static class Platform
{
	public const string DLL = "FosterPlatform";

	[Flags]
	public enum FosterFlags
	{
		FULLSCREEN    = 1 << 0,
		VSYNC         = 1 << 1,
		RESIZABLE     = 1 << 2,
		MOUSE_VISIBLE = 1 << 3,
	}

	[Flags]
	public enum FosterSoundFlags
	{
		STREAM = 0x00000001,
		DECODE = 0x00000002,
		NO_SPATIALIZATION = 0x00004000
	}

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterLogFn(IntPtr msg);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterExitRequestFn();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnTextFn(IntPtr cstr);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnKeyFn(int key, bool pressed);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnMouseButtonFn(int button, bool pressed);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnMouseMoveFn(float posX, float posY);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnMouseWheelFn(float offsetX, float offsetY);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnControllerConnectFn(int index, IntPtr name, int buttonCount, int axisCount, bool isGamepad, ushort vendor, ushort product, ushort version);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnControllerDisconnectFn(int index);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnControllerButtonFn(int index, int button, bool pressed);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterOnControllerAxisFn(int index, int axis, float value);
	
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void FosterWriteFn(IntPtr context, IntPtr data, int size);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterDesc
	{
		public IntPtr windowTitle;
		public IntPtr applicationName;
		public int width;
		public int height;
		public Renderers renderer;
		public FosterFlags flags;
		public FosterLogFn onLogInfo;
		public FosterLogFn onLogWarn;
		public FosterLogFn onLogError;
		public FosterExitRequestFn onExitRequest;
		public FosterOnTextFn onText;
		public FosterOnKeyFn onKey;
		public FosterOnMouseButtonFn onMouseButton;
		public FosterOnMouseMoveFn onMouseMove;
		public FosterOnMouseWheelFn onMouseWheel;
		public FosterOnControllerConnectFn onControllerConnect;
		public FosterOnControllerDisconnectFn onControllerDisconnect;
		public FosterOnControllerButtonFn onControllerButton;
		public FosterOnControllerAxisFn onControllerAxis;
		public int logging;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterVertexElement
	{
		public int index;
		public VertexType type;
		public int normalized;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterVertexFormat
	{
		public IntPtr elements;
		public int elementCount;
		public int stride;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterUniformInfo
	{
		public int index;
		public IntPtr name;
		public UniformType type;
		public int arrayElements;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterShaderData
	{
		public string vertex;
		public string fragment;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterRect
	{
		public int x, y, w, h;

		public FosterRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	public struct FosterDrawCommand
	{
		public IntPtr target;
		public IntPtr mesh;
		public IntPtr shader;
		public int hasViewport;
		public int hasScissor;
		public FosterRect viewport;
		public FosterRect scissor;
		public int indexStart;
		public int indexCount;
		public int instanceCount;
		public DepthCompare compare;
		public CullMode cull;
		public BlendMode blend;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct FosterClearCommand
	{
		public IntPtr target;
		public FosterRect clip;
		public Color color;
		public int depth;
		public int stencil;
		public ClearMask mask;
	}

	public static unsafe string ParseUTF8(IntPtr s)
	{
		byte* ptr = (byte*) s;
		while (*ptr != 0)
			ptr++;
		return System.Text.Encoding.UTF8.GetString((byte*)s, (int)(ptr - (byte*)s));
	}

	public static unsafe IntPtr ToUTF8(in string str)
	{
		var count = Encoding.UTF8.GetByteCount(str) + 1;
		var ptr = Marshal.AllocHGlobal(count);
		var span = new Span<byte>((byte*)ptr.ToPointer(), count);
		Encoding.UTF8.GetBytes(str, span);
		span[^1] = 0;
		return ptr;
	}

	public static void FreeUTF8(IntPtr ptr)
	{
		Marshal.FreeHGlobal(ptr);
	}

	[DllImport(DLL)]
	public static extern void FosterStartup(FosterDesc desc);
	[DllImport(DLL)]
	public static extern void FosterBeginFrame();
	[DllImport(DLL)]
	public static extern void FosterPollEvents();
	[DllImport(DLL)]
	public static extern void FosterEndFrame();
	[DllImport(DLL)]
	public static extern void FosterShutdown();
	[DllImport(DLL)]
	public static extern bool FosterIsRunning();
	[DllImport(DLL)]
	public static extern void FosterSetTitle(string title);
	[DllImport(DLL)]
	public static extern void FosterSetSize(int width, int height);
	[DllImport(DLL)]
	public static extern void FosterGetSize(out int width, out int height);
	[DllImport(DLL)]
	public static extern void FosterGetSizeInPixels(out int width, out int height);
	[DllImport(DLL)]
	public static extern void FosterSetFlags(FosterFlags flags);
	[DllImport(DLL)]
	public static extern IntPtr FosterGetUserPath();
	[DllImport(DLL)]
	public static extern void FosterSetClipboard(string ptr);
	[DllImport(DLL)]
	public static extern IntPtr FosterGetClipboard();
	[DllImport(DLL)]
	public static extern bool FosterGetFocused();
	[DllImport(DLL)]
	public static extern IntPtr FosterImageLoad(IntPtr memory, int length, out int w, out int h);
	[DllImport(DLL)]
	public static extern void FosterImageFree(IntPtr data);
	[DllImport(DLL)]
	public static extern bool FosterImageWrite(FosterWriteFn func, IntPtr context, int w, int h, IntPtr data);
	[DllImport(DLL)]
	public static extern IntPtr FosterFontInit(IntPtr data, int length);
	[DllImport(DLL)]
	public static extern void FosterFontGetMetrics(IntPtr font, out int ascent, out int descent, out int linegap);
	[DllImport(DLL)]
	public static extern int FosterFontGetGlyphIndex(IntPtr font, int codepoint);
	[DllImport(DLL)]
	public static extern float FosterFontGetScale(IntPtr font, float size);
	[DllImport(DLL)]
	public static extern float FosterFontGetKerning(IntPtr font, int glyph1, int glyph2, float scale);
	[DllImport(DLL)]
	public static extern void FosterFontGetCharacter(IntPtr font, int glyph, float scale, out int width, out int height, out float advance, out float offsetX, out float offsetY, out int visible);
	[DllImport(DLL)]
	public static extern void FosterFontGetPixels(IntPtr font, IntPtr dest, int glyph, int width, int height, float scale);
	[DllImport(DLL)]
	public static extern void FosterFontFree(IntPtr font);
	[DllImport(DLL)]
	public static extern Renderers FosterGetRenderer();
	[DllImport(DLL)]
	public static extern IntPtr FosterTextureCreate(int width, int height, TextureFormat format);
	[DllImport(DLL)]
	public static extern void FosterTextureSetData(IntPtr texture, IntPtr data, int length);
	[DllImport(DLL)]
	public static extern void FosterTextureGetData(IntPtr texture, IntPtr data, int length);
	[DllImport(DLL)]
	public static extern void FosterTextureDestroy(IntPtr texture);
	[DllImport(DLL)]
	public static extern IntPtr FosterTargetCreate(int width, int height, TextureFormat[] formats, int formatCount);
	[DllImport(DLL)]
	public static extern IntPtr FosterTargetGetAttachment(IntPtr target, int index);
	[DllImport(DLL)]
	public static extern void FosterTargetDestroy(IntPtr target);
	[DllImport(DLL)]
	public static extern IntPtr FosterShaderCreate(ref FosterShaderData data);
	[DllImport(DLL)]
	public static extern unsafe void FosterShaderGetUniforms(IntPtr shader, FosterUniformInfo* output, out int count, int max);
	[DllImport(DLL)]
	public static extern unsafe void FosterShaderSetUniform(IntPtr shader, int index, float* values);
	[DllImport(DLL)]
	public static extern unsafe void FosterShaderSetTexture(IntPtr shader, int index, IntPtr* values);
	[DllImport(DLL)]
	public static extern unsafe void FosterShaderSetSampler(IntPtr shader, int index, TextureSampler* values);
	[DllImport(DLL)]
	public static extern void FosterShaderDestroy(IntPtr shader);
	[DllImport(DLL)]
	public static extern IntPtr FosterMeshCreate();
	[DllImport(DLL)]
	public static extern void FosterMeshSetVertexFormat(IntPtr mesh, ref FosterVertexFormat format);
	[DllImport(DLL)]
	public static extern void FosterMeshSetVertexData(IntPtr mesh, IntPtr data, int dataSize, int dataDestOffset);
	[DllImport(DLL)]
	public static extern void FosterMeshSetIndexFormat(IntPtr mesh, IndexFormat format);
	[DllImport(DLL)]
	public static extern void FosterMeshSetIndexData(IntPtr mesh, IntPtr data, int dataSize, int dataDestOffset);
	[DllImport(DLL)]
	public static extern void FosterMeshDestroy(IntPtr mesh);
	[DllImport(DLL)]
	public static extern void FosterDraw(ref FosterDrawCommand command);
	[DllImport(DLL)]
	public static extern void FosterClear(ref FosterClearCommand command);
	[DllImport(DLL)]
	public static extern float FosterAudioGetVolume();
	[DllImport(DLL)]
	public static extern void FosterAudioSetVolume(float value);
	[DllImport(DLL)]
	public static extern int FosterAudioGetChannels();
	[DllImport(DLL)]
	public static extern int FosterAudioGetSampleRate();
	[DllImport(DLL)]
	public static extern ulong FosterAudioGetTimePcmFrames();
	[DllImport(DLL)]
	public static extern void FosterAudioSetTimePcmFrames(ulong value);
	[DllImport(DLL)]
	public static extern int FosterAudioGetListenerCount();
	[DllImport(DLL)]
	public static extern IntPtr FosterAudioDecode(IntPtr data, int length, ref AudioFormat format, ref int channels, ref int sampleRate, out ulong decodedFrameCount);
	[DllImport(DLL)]
	public static extern void FosterAudioFree(IntPtr data);
	[DllImport(DLL)]
	public static extern void FosterAudioRegisterEncodedData(string name, IntPtr data, int length);
	[DllImport(DLL)]
	public static extern void FosterAudioRegisterDecodedData(string name, IntPtr data, ulong frameCount, AudioFormat format, int channels, int sampleRate);
	[DllImport(DLL)]
	public static extern void FosterAudioUnregisterData(string name);

	[DllImport(DLL)]
	public static extern bool FosterAudioListenerGetEnabled(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetEnabled(int index, bool value);
	[DllImport(DLL)]
	public static extern Vector3 FosterAudioListenerGetPosition(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetPosition(int index, Vector3 value);
	[DllImport(DLL)]
	public static extern Vector3 FosterAudioListenerGetVelocity(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetVelocity(int index, Vector3 value);
	[DllImport(DLL)]
	public static extern Vector3 FosterAudioListenerGetDirection(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetDirection(int index, Vector3 value);
	[DllImport(DLL)]
	public static extern SoundCone FosterAudioListenerGetCone(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetCone(int index, SoundCone value);
	[DllImport(DLL)]
	public static extern Vector3 FosterAudioListenerGetWorldUp(int index);
	[DllImport(DLL)]
	public static extern void FosterAudioListenerSetWorldUp(int index, Vector3 value);

	[DllImport(DLL)]
	public static extern IntPtr FosterSoundCreate(string path, FosterSoundFlags flags, IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundPlay(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundStop(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundDestroy(IntPtr sound);
	[DllImport(DLL)]
	public static extern float FosterSoundGetVolume(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetVolume(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetPitch(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPitch(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetPan(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPan(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern bool FosterSoundGetPlaying(IntPtr sound);
	[DllImport(DLL)]
	public static extern bool FosterSoundGetFinished(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundGetDataFormat(IntPtr sound, out AudioFormat format, out int channels, out int sampleRate);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLengthPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetCursorPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetCursorPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL)]
	public static extern bool FosterSoundGetLooping(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetLooping(IntPtr sound, bool value);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLoopBeginPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetLoopBeginPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL)]
	public static extern ulong FosterSoundGetLoopEndPcmFrames(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetLoopEndPcmFrames(IntPtr sound, ulong value);
	[DllImport(DLL)]
	public static extern bool FosterSoundGetSpatialized(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetSpatialized(IntPtr sound, bool value);
	[DllImport(DLL)]
	public static extern Vector3 FosterSoundGetPosition(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPosition(IntPtr sound, Vector3 value);
	[DllImport(DLL)]
	public static extern Vector3 FosterSoundGetVelocity(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetVelocity(IntPtr sound, Vector3 value);
	[DllImport(DLL)]
	public static extern Vector3 FosterSoundGetDirection(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetDirection(IntPtr sound, Vector3 value);
	[DllImport(DLL)]
	public static extern SoundPositioning FosterSoundGetPositioning(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPositioning(IntPtr sound, SoundPositioning value);
	[DllImport(DLL)]
	public static extern int FosterSoundGetPinnedListenerIndex(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetPinnedListenerIndex(IntPtr sound, int value);
	[DllImport(DLL)]
	public static extern SoundAttenuationModel FosterSoundGetAttenuationModel(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetAttenuationModel(IntPtr sound, SoundAttenuationModel value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetRolloff(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetRolloff(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMinGain(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMinGain(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMaxGain(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMaxGain(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMinDistance(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMinDistance(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetMaxDistance(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetMaxDistance(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern SoundCone FosterSoundGetCone(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetCone(IntPtr sound, SoundCone value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetDirectionalAttenuationFactor(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetDirectionalAttenuationFactor(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGetDopplerFactor(IntPtr sound);
	[DllImport(DLL)]
	public static extern void FosterSoundSetDopplerFactor(IntPtr sound, float value);
	[DllImport(DLL)]
	public static extern IntPtr FosterSoundGroupCreate(IntPtr parent);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupDestroy(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern float FosterSoundGroupGetVolume(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupSetVolume(IntPtr soundGroup, float value);
	[DllImport(DLL)]
	public static extern float FosterSoundGroupGetPitch(IntPtr soundGroup);
	[DllImport(DLL)]
	public static extern void FosterSoundGroupSetPitch(IntPtr soundGroup, float value);
}
