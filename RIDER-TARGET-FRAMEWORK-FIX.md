# Rider Target Framework Fix - Step by Step

## Problem
Rider shows "Error: Target framework is not specified" in the run configuration, even though the project file has `<TargetFramework>net8.0</TargetFramework>`.

## Solution Steps

### Step 1: Close Rider Completely
Make sure Rider is fully closed (not just the project window).

### Step 2: In Terminal, Run These Commands

```bash
# Navigate to project directory
cd /Users/vicperdana/coderepo/sideprojects/AspireShopWithSemanticKernel

# Shutdown all build servers
dotnet build-server shutdown

# Clean and restore
dotnet clean
dotnet restore

# Build to verify everything works
dotnet build
```

### Step 3: Fix the Run Configuration in Rider

**Option A: Manual Fix (In Rider UI)**

1. Open Rider
2. Open `AspireShop.sln`
3. Wait for Rider to finish indexing (watch progress bar at bottom)
4. Click **Run** → **Edit Configurations...**
5. In the "Target framework" dropdown:
   - Click the dropdown
   - It should show `net8.0` 
   - If empty, type: `net8.0` or select it from the list
6. Click **Apply**
7. Click **Run**

**Option B: Delete and Recreate Configuration**

1. Open Rider
2. Open `AspireShop.sln`  
3. Click **Run** → **Edit Configurations...**
4. Select `AspireShop.AppHost: http` and click the `-` button to delete it
5. Click `+` → **.NET Project**
6. Select:
   - **Project:** AspireShop.AppHost
   - **Target framework:** net8.0 (should auto-detect now)
   - **Name:** AspireShop.AppHost
7. Click **Apply**
8. Click **Run**

**Option C: Command Line Approach (Recommended)**

Instead of using Rider's run configuration, run directly from terminal:

```bash
# Navigate to AppHost directory
cd AspireShop.AppHost

# Run the Aspire app
dotnet run
```

This will:
- Start the Aspire dashboard
- Open browser automatically to http://localhost:15888 (or similar)
- Show all services in the dashboard

### Step 4: Verify Rider Settings

1. In Rider, go to **File** → **Settings** (or **Preferences** on Mac)
2. Navigate to **Build, Execution, Deployment** → **.NET Core SDK**
3. Ensure "Use SDK from global.json if present" is **checked**
4. The SDK path should be: `/usr/local/share/dotnet`
5. Click **OK**

### Step 5: Invalidate Caches (Last Resort)

If still not working:

1. In Rider: **File** → **Invalidate Caches**
2. Select all checkboxes
3. Click **Invalidate and Restart**
4. After restart, reopen the solution

## Quick Test from Terminal

To verify everything works without Rider:

```bash
# From project root
cd AspireShop.AppHost
dotnet run

# Expected output:
# Building...
# Now listening on: https://localhost:7185
# Aspire Dashboard: http://localhost:15888
```

If this works, the issue is purely Rider's project model, not your code.

## Why This Happens

Rider caches project information and sometimes doesn't detect framework changes properly. Common causes:

1. **.NET SDK cache** - Rider's internal cache doesn't match installed SDKs
2. **Solution file changes** - Solution was modified while Rider was open
3. **Multiple SDK versions** - Rider gets confused with multiple .NET versions
4. **Aspire workload** - Special handling needed for Aspire projects

## Your Current Setup ✅

- .NET 8.0.204 SDK: Installed ✅
- Target Framework: net8.0 ✅  
- Aspire Workload: Installed (8.2.2) ✅
- Project File: Correct ✅
- Build from CLI: Works ✅

**The issue is Rider's UI, not your project!**

## Alternative: Use Terminal + Rider for Editing

You can:
1. **Edit code in Rider** (full IntelliSense, refactoring, etc.)
2. **Run from terminal** using `dotnet run` in `AspireShop.AppHost`
3. **Debug** by attaching Rider's debugger to the running process:
   - Run → Attach to Process
   - Filter by "AspireShop.AppHost"
   - Select and attach

This gives you all Rider features without fighting the run configuration!

## Expected Behavior After Fix

Once fixed, you should see:
- ✅ "Target framework" dropdown shows: **net8.0**
- ✅ Run button is enabled (green triangle)
- ✅ Clicking Run starts the Aspire dashboard
- ✅ Browser opens to dashboard URL

## Still Not Working?

Try this nuclear option:

```bash
# Close Rider completely
# Then delete ALL Rider caches:
rm -rf .idea/
rm -rf ~/.cache/JetBrains/Rider*/

# Open Rider fresh and open the solution
```

This forces Rider to rebuild everything from scratch.
