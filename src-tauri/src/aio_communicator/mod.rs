use crossbeam_channel::Receiver;
use std::time::Duration;
use std::thread;
use log::{info, error, warn};

pub mod constants;
pub mod device;
pub mod global_mutex;

use device::CorsairH150i;

pub fn run_aio_loop(rx: Receiver<Vec<u8>>) {
    info!("[RUST] Starting AIO Loop...");

    let mut device_opt: Option<CorsairH150i> = None;

    loop {
        match rx.recv_timeout(Duration::from_secs(10)) {
            Ok(jpeg_frame) => {
                if device_opt.is_none() {
                    match CorsairH150i::new() {
                        Ok(d) => {
                            info!("[RUST] Corsair Device Connected!");
                            device_opt = Some(d);
                        },
                        Err(e) => {
                            error!("[RUST] Device not found: {}", e);
                            thread::sleep(Duration::from_millis(2000));
                            continue;
                        }
                    }
                }

                if let Some(device) = &device_opt {
                    if let Err(e) = device.send_image(&jpeg_frame) {
                        let err_msg = e.to_string();
                        if err_msg.contains("Mutex timeout") {
                            // Don't drop connection on lock contention, just skip this frame
                            warn!("[RUST] Frame skipped (Mutex locked)");
                        } else {
                            error!("[RUST] Write error (Dropping connection): {}", e);
                            device_opt = None;
                        }
                    }
                }
            }
            Err(_) => {
                if device_opt.is_some() {
                    info!("[RUST] Idle timeout (10s) - Closing device connection.");
                    device_opt = None;
                }
            }
        }
    }
}