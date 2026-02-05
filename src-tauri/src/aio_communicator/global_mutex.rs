use windows_sys::Win32::System::Threading::{CreateMutexW, WaitForSingleObject, ReleaseMutex};
use windows_sys::Win32::Foundation::{HANDLE, CloseHandle, FALSE, WAIT_TIMEOUT, WAIT_OBJECT_0, WAIT_ABANDONED};
use std::ffi::OsStr;
use std::os::windows::ffi::OsStrExt;
use std::io;

pub struct GlobalMutex {
    handle: HANDLE,
}

impl GlobalMutex {
    pub fn new(name: &str) -> io::Result<Self> {
        let mut wide: Vec<u16> = OsStr::new(name).encode_wide().collect();
        wide.push(0);
        
        // CreateMutexW will open the existing mutex if it exists
        let handle = unsafe { CreateMutexW(std::ptr::null(), FALSE, wide.as_ptr()) };
        
        if handle == 0 {
            return Err(io::Error::last_os_error());
        }
        
        Ok(Self { handle })
    }

    pub fn lock(&self) -> io::Result<MutexGuard<'_>> {
        // Wait up to 1000ms. If we can't get it, we assume it's busy and fail to avoid freezing the UI thread (though this is running in a potentially async task, blocking here is bad if it's long).
        // However, standard CreateMutex behavior is usually accompanied by ReleaseMutex.
        // Wait up to 3000ms to be safe (C# uses 1000ms+transactions).
        let result = unsafe { WaitForSingleObject(self.handle, 3000) };
        
        if result == WAIT_OBJECT_0 || result == WAIT_ABANDONED {
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
