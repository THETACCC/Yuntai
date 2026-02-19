# AudioManager

Unity 音频管理器，基于 AudioMixer 实现分组音量控制，支持按需加载音频资源。

## 环境要求

- 在 Resources/ 目录下放置名为 MainAudioMixer 的 AudioMixer 资源
- AudioMixer 需包含以下 Group：Master、Music、SFX、UI
- AudioMixer 需暴露以下参数：MasterVolume、MusicVolume、SFXVolume

## 使用方式

音频文件需放置在 Resources/ 目录下，调用时传入文件名（不含扩展名）。

------------------------------------------------------------
播放音频
------------------------------------------------------------

// 播放（使用默认 Master 组，或上次指定的组）
AudioManager.Play("bgm_main");

// 播放并指定音频组
AudioManager.Play("sfx_jump", AudioManager.AudioGroup.SFX);

// 单次播放（不循环打断）
AudioManager.PlayOneShot("sfx_click");
AudioManager.PlayOneShot("sfx_click", AudioManager.AudioGroup.UI);

// 播放音乐，支持循环
AudioManager.PlayMusic("bgm_main", loop: true);

------------------------------------------------------------
停止 / 音量
------------------------------------------------------------

AudioManager.Stop("bgm_main");

// volume 范围 0~1
AudioManager.SetVolume("bgm_main", 0.5f);

------------------------------------------------------------
全局音量控制（传入范围 0~100，内部自动转换为 dB 值）
------------------------------------------------------------

AudioManager.SetMasterVolume(80f);
AudioManager.SetMusicVolume(60f);
AudioManager.SetSFXVolume(100f);

------------------------------------------------------------
获取 AudioSource
------------------------------------------------------------

AudioSource source = AudioManager.GetAudioSource("bgm_main");

------------------------------------------------------------
