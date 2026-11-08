# Opening/building the Quest project

Use Visual Studio 2022 **17.8 or newer** with .NET 8 SDK support, or the .NET 8
SDK command line. This is a desktop build of a managed mod DLL; no Android
workload is required. The game itself supplies Unity and the Quest IL2CPP runtime.

The project uses `Microsoft.NET.Sdk` with `<TargetFramework>net8.0</TargetFramework>`.
Do not replace it with the legacy `ToolsVersion` / `TargetFrameworkVersion=v8.0`
project. Do not use PC's .NET Framework 4.7.2 project for Quest.

1. Keep the checkout's `quest`, `shared`, and `armorstand` folders together.
2. Open `quest/ArmorRestored.csproj` (or reload it in your solution).
3. Supply **your Quest installation's matching generated interop assemblies**, its
   Unity reference assemblies, MelonLoader and Il2CppInterop.Runtime, and 0Harmony.
   The DLL names required directly are listed under `<Reference>` in the project.
   Copy these and their needed reference dependencies into `refs/quest` at the
   repository root. Do not use the PC game's Managed DLLs or PC MelonLoader DLLs.
   Reference DLLs are local-only and must not be committed or shipped with the mod.
4. Alternatively, point to an existing folder containing those references with a
   `quest/Quest.Local.props` file (ignored by Git):

   ```xml
   <Project>
     <PropertyGroup>
       <QuestReferenceDir>C:\MyQuestReferences</QuestReferenceDir>
     </PropertyGroup>
   </Project>
   ```

   If references are spread across folders, you can instead edit their individual
   `HintPath` entries to point to those exact DLLs.
5. Build Release. On the command line, from the repository root:

   ```text
   dotnet build quest/ArmorRestored.csproj -c Release
   ```

   Output: `quest/bin/Release/ArmorRestored.dll`. Both armor/stand resources are
   embedded. Install only the resulting mod DLL, replacing/removing the previous
   Quest armor DLL so the mod isn't loaded twice. Server and clients need 0.4.0.

The .NET 8 SDK supplies framework reference assemblies itself. Do **not** manually
reference `System.Runtime`, `System.Collections`, etc. from a hard-coded
`Program Files/dotnet/shared/.../8.0.27` runtime directory.

`EnableDefaultItems=false` retains the explicit source/resource lists without
SDK duplicate-item errors. `GenerateAssemblyInfo=false` retains the existing
`Properties/AssemblyInfo.cs` without duplicate assembly attributes.
`ARMOR_QUEST` selects the correct side of the shared armor-stand code.

Missing game references normally appear as build/reference errors after the
project opens. If the project still cannot **load**, copy the exact error from
Visual Studio's reload dialog/Output window and include your Visual Studio version
and `dotnet --list-sdks` output. XML validation alone does not verify IDE loading
or compilation; this repository does not include proprietary game references.
