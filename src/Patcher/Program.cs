using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private static string Hash(string path) { using (var sha = SHA256.Create()) using (var s = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", ""); }
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 4) throw new ArgumentException("Expected original Assembly-CSharp.dll, original URP.dll, runtime DLL, output directory.");
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
            resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2])));
            var read = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false, InMemory = true };
            Directory.CreateDirectory(args[3]);
            using (var runtime = AssemblyDefinition.ReadAssembly(args[2], read))
            using (var game = AssemblyDefinition.ReadAssembly(args[0], read))
            using (var urp = AssemblyDefinition.ReadAssembly(args[1], read))
            {
                var hooks = runtime.MainModule.Types.Single(t => t.FullName == "BioEden.NoDOF.Runtime");
                var registration = hooks.Methods.Single(m => m.Name == "Register");
                var enabled = hooks.Methods.Single(m => m.Name == "get_Enabled");
                var getSettings = game.MainModule.Types.Single(t => t.FullName == "Biomes.Settings.SettingsManager")
                    .Methods.Single(m => m.Name == "GetSettingsData");
                if (game.MainModule.AssemblyReferences.Any(r => r.Name == "BioEden.NoDOF")) throw new InvalidDataException("Already patched game assembly.");
                var returns = getSettings.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
                if (returns.Length != 1) throw new InvalidDataException("Unexpected settings method layout.");
                var ret = returns[0];
                // Retarget any incoming branches to the hook, preserving the dictionary on stack.
                ret.OpCode = OpCodes.Ldarg_0;
                var il = getSettings.Body.GetILProcessor();
                var call = il.Create(OpCodes.Call, game.MainModule.ImportReference(registration));
                il.InsertAfter(ret, call);
                il.InsertAfter(call, il.Create(OpCodes.Ret));
                getSettings.Body.MaxStackSize += 1;

                var zoomOut = game.MainModule.Types.Single(t => t.FullName == "Biomes.Cam.CameraModuleZoom")
                    .Methods.Single(m => m.Name == "get_ZoomOut");
                var zoomRet = zoomOut.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
                zoomRet.OpCode = OpCodes.Ldarg_0;
                var zoomIl = zoomOut.Body.GetILProcessor();
                var zoomCall = zoomIl.Create(OpCodes.Call, game.MainModule.ImportReference(hooks.Methods.Single(m => m.Name == "ExtendZoom")));
                zoomIl.InsertAfter(zoomRet, zoomCall);
                zoomIl.InsertAfter(zoomCall, zoomIl.Create(OpCodes.Ret));
                zoomOut.Body.MaxStackSize += 1;

                foreach (string limit in new[] { "Min", "Max" })
                {
                    var pitch = game.MainModule.Types.Single(t => t.FullName == "Biomes.Cam.CameraModulePan")
                        .Methods.Single(m => m.Name == "Pitch" + limit);
                    if (pitch.Parameters.Count != 1 || pitch.Parameters[0].ParameterType.FullName != "System.Single")
                        throw new InvalidDataException("Unexpected pitch signature.");
                    var pitchRet = pitch.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
                    pitchRet.OpCode = OpCodes.Ldarg_0;
                    var pitchIl = pitch.Body.GetILProcessor();
                    var loadZoom = pitchIl.Create(OpCodes.Ldarg_1);
                    var pitchCall = pitchIl.Create(OpCodes.Call, game.MainModule.ImportReference(hooks.Methods.Single(m => m.Name == "AdjustPitch" + limit)));
                    pitchIl.InsertAfter(pitchRet, loadZoom);
                    pitchIl.InsertAfter(loadZoom, pitchCall);
                    pitchIl.InsertAfter(pitchCall, pitchIl.Create(OpCodes.Ret));
                    pitch.Body.MaxStackSize = Math.Max(pitch.Body.MaxStackSize, 3);
                }

                var moveBorder = game.MainModule.Types.Single(t => t.FullName == "Biomes.HexGrid.HexAreaBorderManager")
                    .Methods.Single(m => m.Name == "MoveBorder");
                if (moveBorder.Parameters.Count != 6 || moveBorder.Parameters[3].ParameterType.FullName != "UnityEngine.MeshFilter")
                    throw new InvalidDataException("Unexpected border signature.");
                var borderRet = moveBorder.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
                borderRet.OpCode = OpCodes.Ldarg_0;
                var borderIl = moveBorder.Body.GetILProcessor();
                var loadFilter = borderIl.Create(OpCodes.Ldarg, moveBorder.Parameters[3]);
                var borderCall = borderIl.Create(OpCodes.Call, game.MainModule.ImportReference(hooks.Methods.Single(m => m.Name == "RefreshBorder")));
                borderIl.InsertAfter(borderRet, loadFilter);
                borderIl.InsertAfter(loadFilter, borderCall);
                borderIl.InsertAfter(borderCall, borderIl.Create(OpCodes.Ret));
                moveBorder.Body.MaxStackSize = Math.Max(moveBorder.Body.MaxStackSize, 2);

                var isActive = urp.MainModule.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.DepthOfField")
                    .Methods.Single(m => m.Name == "IsActive");
                if (isActive.Body.Instructions.Count < 10 || !isActive.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "mode"))
                    throw new InvalidDataException("Expected original depth-of-field method, not the v1 fixed-Off patch.");
                il = isActive.Body.GetILProcessor();
                var first = isActive.Body.Instructions[0];
                il.InsertBefore(first, il.Create(OpCodes.Call, urp.MainModule.ImportReference(enabled)));
                il.InsertBefore(first, il.Create(OpCodes.Brtrue, first));
                il.InsertBefore(first, il.Create(OpCodes.Ldc_I4_0));
                il.InsertBefore(first, il.Create(OpCodes.Ret));
                var write = new WriterParameters { WriteSymbols = false, DeterministicMvid = true, Timestamp = 0 };
                var gameOut = Path.Combine(args[3], "Assembly-CSharp.dll");
                var urpOut = Path.Combine(args[3], "Unity.RenderPipelines.Universal.Runtime.dll");
                game.Write(gameOut, write);
                urp.Write(urpOut, write);
                Console.WriteLine("Assembly-CSharp.dll " + Hash(gameOut));
                Console.WriteLine("Unity.RenderPipelines.Universal.Runtime.dll " + Hash(urpOut));
            }
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
