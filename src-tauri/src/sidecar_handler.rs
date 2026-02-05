use tauri::{AppHandle, Emitter};
use log::{info, error, debug, warn};
use std::process::Command;
use std::time::Duration;
use std::net::UdpSocket;
use std::thread;
use std::env;
use std::path::PathBuf;
use std::os::windows::process::CommandExt;

// Windows constant to hide the console window created by PowerShell
const CREATE_NO_WINDOW: u32 = 0x08000000;

pub fn spawn_sensor_bridge(app: AppHandle, debug_mode: bool) {
    let binary_name = "lhm-bridge.exe";
    let suffixed_binary_name = "lhm-bridge-x86_64-pc-windows-msvc.exe";
    let current_exe = env::current_exe().unwrap_or_default();
    let exe_dir = current_exe.parent().unwrap_or(&current_exe);

    debug!("Current EXE: {:?}", current_exe);
    debug!("EXE Dir: {:?}", exe_dir);

    let resolve_path = |name: &str| {
        let path_a = exe_dir.join(name);
        let path_b = exe_dir.join("binaries").join(name);
        let path_c = exe_dir.parent().unwrap_or(exe_dir).join("binaries").join(name);
        
        debug!("Checking: {:?}", path_a);
        if path_a.exists() { return Some(path_a); }
        debug!("Checking: {:?}", path_b);
        if path_b.exists() { return Some(path_b); }
        debug!("Checking: {:?}", path_c);
        if path_c.exists() { return Some(path_c); }
        
        None
    };

    let final_path = resolve_path(binary_name)
        .or_else(|| {
            debug!("Base binary not found, checking for suffixed version...");
            resolve_path(suffixed_binary_name)
        })
        .unwrap_or_else(|| {
            warn!("Binary not found in standard locations, falling back to name only.");
            PathBuf::from(binary_name)
        });

    let final_binary_name = final_path.file_name()
        .and_then(|n| n.to_str())
        .unwrap_or(binary_name)
        .to_string();

    let bridge_path = final_path.to_string_lossy()
        .strip_prefix("\\\\?\\")
        .unwrap_or(&final_path.to_string_lossy())
        .to_string();

    info!("Sidecar path resolved: {} (Name: {})", bridge_path, final_binary_name);

    thread::spawn(move || {
        loop {
            let output = Command::new("tasklist")
                .args(&["/FI", &format!("IMAGENAME eq {}", final_binary_name), "/NH"])
                .creation_flags(CREATE_NO_WINDOW)
                .output();

            let is_running = match output {
                Ok(o) => String::from_utf8_lossy(&o.stdout).contains(&final_binary_name),
                Err(_) => false,
            };

            if !is_running {
                let debug_flag = if debug_mode { " --debug" } else { "" };
                
                info!("Spawning sidecar: {} {}", bridge_path, debug_flag);

                let ps_command = if debug_mode {
                    format!("Start-Process -FilePath '{}' -ArgumentList '--debug' -Verb RunAs -WindowStyle Hidden", bridge_path.replace("'", "''"))
                } else {
                    format!("Start-Process -FilePath '{}' -Verb RunAs -WindowStyle Hidden", bridge_path.replace("'", "''"))
                };

                let _ = Command::new("powershell")
                    .args(&["-NoProfile", "-WindowStyle", "Hidden", "-Command", &ps_command])
                    .creation_flags(CREATE_NO_WINDOW)
                    .spawn();
                
                thread::sleep(Duration::from_secs(5));
            }

            thread::sleep(Duration::from_secs(5));
        }
    });

    let app_clone = app.clone();
    thread::spawn(move || {
        let socket = match UdpSocket::bind("127.0.0.1:14242") {
            Ok(s) => s,
            Err(e) => {
                error!("UDP Bind failed: {}", e);
                return;
            }
        };
        
        let _ = socket.set_read_timeout(Some(Duration::from_secs(2)));
        let mut buf = [0u8; 65535];

        loop {
            if let Ok((amt, _)) = socket.recv_from(&mut buf) {
                if let Ok(json_str) = std::str::from_utf8(&buf[..amt]) {
                    let _ = app_clone.emit("sensors-update", json_str);
                }
            }
        }
    });

    thread::spawn(move || {
        let socket = UdpSocket::bind("127.0.0.1:0").unwrap();
        loop {
            let _ = socket.send_to(b"ping", "127.0.0.1:14243");
            thread::sleep(Duration::from_secs(3));
        }
    });
}