#SingleInstance Force
SetWorkingDir A_ScriptDir

; ============================================================
; LOGGER CLASS
; ============================================================
class Logger {
    __logBuffer := ""
    debugLog := A_ScriptDir "\debug_log.txt"

    Log(msg, level := "INFO", flush := false) {
        this.__logBuffer .= FormatTime(, "yyyy-MM-dd HH:mm:ss") " [" level "] " msg "`n"
        if flush || StrLen(this.__logBuffer) > 65536
            this.Flush()
    }

    Flush() {
        if (this.__logBuffer != "") {
            FileAppend(this.__logBuffer, this.debugLog, "UTF-8")
            this.__logBuffer := ""
        }
    }

    Clear() {
        if FileExist(this.debugLog)
            FileDelete(this.debugLog)
    }
}

; ============================================================
; CONFIG MANAGER CLASS
; ============================================================
class ConfigManager {
    c := Map()

    ResolvePath(p) {
        return (p != "" && !InStr(p, ":"))
            ? A_ScriptDir "\" p
            : p
    }

    LoadIniFile(path) {
        ini := Map(), section := ""
        
        for line in StrSplit(FileRead(path), "`n", "`r") {
            line := Trim(line)
            
            if (line = "" || SubStr(line, 1, 1) = ";")
                continue
                
            if (SubStr(line, 1, 1) = "[" && SubStr(line, -1) = "]") {
                section := SubStr(line, 2, -1)
                ini[section] := Map()
                continue
            }
            
            pos := InStr(line, "=")
            
            if (pos && section) {
                key := Trim(SubStr(line, 1, pos - 1))
                val := Trim(SubStr(line, pos + 1))
                ini[section][key] := val
            }
        }
        return ini
    }

    LoadConfig() {
        iniPath := A_ScriptDir "\config.ini"
        if !FileExist(iniPath) {
            MsgBox "Config file not found: " iniPath
            ExitApp
        }

        ini := this.LoadIniFile(iniPath)
        selected := ""

        for section, data in ini {
            if InStr(section, "config:") && data.Get("default", "") = "true" {
                if selected {
                    MsgBox("Multiple sections marked default")
                    ExitApp
                }
                selected := section
            }
        }
        if !selected
            selected := "config:local"

        section := ini.Get(selected, Map())
        Read(k, fallback := "") {
            return section.Has(k) ? section[k] : fallback
        }

        ; Build config map
        this.c["mode"] := Read("mode", "offline")
        this.c["server"] := this.ResolvePath(Read("server"))
        this.c["launcher"] := this.ResolvePath(Read("client"))
        this.c["cache_dir"] := this.ResolvePath(Read("cache_dir"))
        this.c["address"] := Read("address")
        this.c["username"] := Read("username")
        this.c["token"] := Read("token")
        this.c["log_file"] := this.ResolvePath(Read("log_file"))
        this.c["verbose"] := Read("verbose")
        this.c["dxvk_hud"] := Read("dxvk_hud", "false")
        fps := Read("fps_limit")
        this.c["fps_limit"] := fps ? String(fps) : "60"
        this.c["graphics_api"] := Read("graphics_api")
        this.c["fullscreen"] := Read("fullscreen", "true")

        SplitPath(this.c["server"], , &serverDir)
        SplitPath(this.c["launcher"], , &launcherDir)
        this.c["server_dir"] := serverDir
        this.c["launcher_dir"] := launcherDir
    }
}

; ============================================================
; GAME RUNTIME CLASS
; ============================================================
class GameRuntime {
    serverPid := 0
    clientPid := 0
    logger := Logger()
    config := ConfigManager()

    Quote(x) => '"' x '"'

    AddArg(&args, flag, val) {
        if val
            args .= " " flag " " this.Quote(val)
    }

	BuildEnvironment() {
		c := this.config.c
		
		; --- FPS Cap ---
		if (c["fps_limit"] != "")
	        EnvSet("DXVK_FRAME_RATE", c["fps_limit"]) ; Better DXVK frame limiter

		; --- Optional HUD ---
		if (c["dxvk_hud"] = "true")
			EnvSet("DXVK_HUD", "full")
	}

    StartServer() {
        c := this.config.c
        this.logger.Log("Starting server...")
        
        Run(c["server"], c["server_dir"], , &this.serverPid)
        
        if !this.serverPid {
            MsgBox "Server failed to start"
            ExitApp
        }
    }

    BuildClientArgs() {
        c := this.config.c
        cache := "file:///" StrReplace(c["cache_dir"],"\","/") "/"
        
        args := ""
        args .= ' -m ' this.Quote(cache "main.unity3d")
        args .= ' --asseturl ' this.Quote(cache)
        args .= ' -a ' this.Quote(c["address"])
        
        this.AddArg(&args, "--username", c["username"])
        this.AddArg(&args, "--token", c["token"])
        this.AddArg(&args, "-l", c["log_file"])
        
        if (c["graphics_api"] = "opengl")
            args .= " --force-opengl"
        if (c["graphics_api"] = "vulkan")
            args .= " --force-vulkan"
        if (c["fullscreen"] = "true") {
            this.AddArg(&args, "--width", A_ScreenWidth)
            this.AddArg(&args, "--height", A_ScreenHeight)
        }
        if c["verbose"] = "true"
            args .= " -v"
            
        return args
    }

    SpawnClient() {
		c := this.config.c
		args := this.BuildClientArgs()
		this.BuildEnvironment()

		Run(c["launcher"] " " args, c["launcher_dir"], , &this.clientPid)
		if !this.clientPid {
			this.logger.Log("Client spawn FAILED", "ERROR", true)
			MsgBox "Client spawn failed"
			ExitApp
		}
		this.logger.Log("Client PID: " this.clientPid, "INFO", true)
	}

	ApplyFullscreen() {
		Loop 50 {
			hwnd := WinExist("ahk_pid " this.clientPid)
			
			if hwnd {
				WinSetStyle("-0xC40000", hwnd)
				WinMove(0, 0, A_ScreenWidth, A_ScreenHeight, hwnd)
				WinSetAlwaysOnTop(1, hwnd)
				
				return
			}
			Sleep(200)
		}
	}

    WaitClient() {
        ProcessWaitClose(this.clientPid)
        
        if this.serverPid {
            if ProcessExist(this.serverPid)
                ProcessClose(this.serverPid)
        }
        
        ExitApp
    }

    Run() {
        this.logger.Clear()
        this.config.LoadConfig()
        
        if (this.config.c["mode"] = "offline")
            this.StartServer()
            
        this.SpawnClient()
        
        if (this.config.c["fullscreen"] = "true")
            this.ApplyFullscreen()
            
        this.WaitClient()
    }
}

; ============================================================
; ENTRYPOINT
; ============================================================
GameRuntime().Run()
