use windows_sys::Win32::System::Threading::{CreateMutexW, WaitForSingleObject, ReleaseMutex};
use windows_sys::Win32::Foundation::{HANDLE, CloseHandle, FALSE, WAIT_TIMEOUT, WAIT_OBJECT_0, WAIT_ABANDONED};
use std::ffi::OsStr;
use std::os::windows::ffi::OsStrExt;
use std::io;
use log::warn;

/// A cross-process mutex using Windows Named Mutexes.
pub struct GlobalMutex {
    handle: HANDLE,
}

impl GlobalMutex {
    /// Create or open a named mutex.
    pub fn new(name: &str) -> io::Result<Self> {
        let mut wide: Vec<u16> = OsStr::new(name).encode_wide().collect();
        wide.push(0);
        
        // CreateMutexW will open the existing mutex if it exists.
        let handle = unsafe { CreateMutexW(std::ptr::null(), FALSE, wide.as_ptr()) };
        
        if handle == 0 {
            return Err(io::Error::last_os_error());
        }
        
        Ok(Self { handle })
    }

    /// Acquire the lock.
    /// 
    /// Waits up to 3000ms. If it fails, it assumes the mutex is busy to avoid freezing the caller.
    pub fn lock(&self) -> io::Result<MutexGuard<'_>> {
        // Wait up to 3000ms to be safe (C# uses 1000ms+transactions).
        let result = unsafe { WaitForSingleObject(self.handle, 3000) };
        
        if result == WAIT_OBJECT_0 {
            Ok(MutexGuard { mutex: self })
        } else if result == WAIT_ABANDONED {
            warn!("Global mutex acquired with WAIT_ABANDONED — previous owner likely crashed without releasing");
            Ok(MutexGuard { mutex: self })
        } else if result == WAIT_TIMEOUT {
             Err(io::Error::new(io::ErrorKind::TimedOut, "Mutex timeout"))
        } else {
             Err(io::Error::last_os_error())
        }
    }
}

pub struct MutexGuard<'a> {
    mutex: &'a GlobalMutex,
}

impl<'a> Drop for MutexGuard<'a> {
    fn drop(&mut self) {
        unsafe { ReleaseMutex(self.mutex.handle) };
    }
}

impl Drop for GlobalMutex {
    fn drop(&mut self) {
        unsafe { CloseHandle(self.handle) };
    }
}
