namespace Foster.Framework.Audio;

internal record struct SoundInstanceState(IntPtr Ptr, Sound Sound, SoundGroup? Group, bool Protected);
