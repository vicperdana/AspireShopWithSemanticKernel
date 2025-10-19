# Fixing Rider "TargetFramework is not available" Error

## Problem
When opening AspireShop.sln in JetBrains Rider, you get an error: "TargetFramework is not available"

## Root Cause
Rider's cache may be out of sync with your .NET SDK installations, or it needs to re-detect the .NET frameworks available on your system.

## Solution Applied

### Step 1: Clear Rider Cache
```bash
rm -rf .idea/
```

### Step 2: Clean and Restore Solution
```bash
dotnet clean
dotnet restore
```

### Step 3: Reopen in Rider
1. Close Rider completely (if it's open)
2. Open Rider
3. Open the solution: `AspireShop.sln`

## If Issue Persists

### Option 1: Invalidate Rider Caches
1. In Rider, go to **File** → **Invalidate Caches**
2. Select **Invalidate and Restart**

### Option 2: Configure .NET SDK Path in Rider
1. Go to **File** → **Settings** (or **Rider** → **Preferences** on macOS)
2. Navigate to **Build, Execution, Deployment** → **.NET Core SDK**
3. Ensure the path points to: `/usr/local/share/dotnet/`
4. Rider should detect these SDKs:
   - .NET 6.0.129
   - .NET 8.0.200
   - .NET 8.0.204 ✅ (required)
   - .NET 9.0.203

### Option 3: Update Rider
Make sure you're running the latest version of Rider, as older versions may have issues with .NET 8/9:
1. Go to **Help** → **Check for Updates**
2. Install any available updates

## Verification

After reopening Rider, you should see:
- ✅ All projects load without errors
- ✅ No "TargetFramework is not available" messages
- ✅ Solution builds successfully (0 errors, 48 warnings expected)

## Your Current Setup

- **Solution:** AspireShop.sln
- **Target Framework:** net8.0
- **Installed SDKs:**
  - .NET 8.0.204 (matches project target)
  - .NET 9.0.203 (available but not used)
- **Global SDK Version:** 8.0.* with rollForward: major

## Build Verification

After opening in Rider, verify the build works:

1. **In Rider:**
   - Click **Build** → **Build Solution**
   - Expected: 0 Errors, 48 Warnings

2. **Or in Terminal:**
   ```bash
   dotnet build
   # Expected: Build succeeded
   ```

## Run Configuration

To run the Aspire App Host in Rider:

1. Look for run configuration dropdown (top right)
2. Select **AspireShop.AppHost**
3. Click the green Run button
4. The Aspire dashboard should open in your browser

## Common Issues

### Issue: "SDK not found"
**Solution:** Ensure `/usr/local/share/dotnet` is in your PATH:
```bash
echo $PATH | grep dotnet
```

### Issue: "Multiple SDKs targeting same framework"
**Solution:** This is normal. The latest compatible SDK (8.0.204) will be used.

### Issue: "NuGet packages not restored"
**Solution:** Right-click solution → **Restore NuGet Packages**

## Status

✅ **Cache cleared** - `.idea/` folder removed  
✅ **Solution cleaned** - All build artifacts removed  
✅ **Packages restored** - All NuGet packages downloaded  
✅ **Ready to open in Rider** - Should work now

## Next Steps

1. Open Rider
2. Open `AspireShop.sln`
3. Wait for Rider to index the solution (~1-2 minutes)
4. Try building (Ctrl+Shift+B or Cmd+Shift+B)
5. If successful, you're ready to develop!
