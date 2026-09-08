using Mono.Cecil;
using Mono.Cecil.Cil;

string managed = args[0];
GeometryChecks.Run();
WaterChecks.Run();
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types) => types.SelectMany(t => new[] {t}.Concat(Types(t.NestedTypes)));
string Operand(object operand, Mono.Cecil.Cil.MethodBody body) => operand switch {
    null => "", Instruction i => "ILINDEX:" + body.Instructions.IndexOf(i),
    Instruction[] xs => string.Join(",", xs.Select(i => Operand(i, body))),
    MemberReference r => r.FullName, VariableDefinition v => "LOCAL:" + v.Index + ":" + v.VariableType.FullName,
    ParameterDefinition p => "PARAM:" + p.Index + ":" + p.ParameterType.FullName,
    _ => operand.ToString()!
};
string Body(MethodDefinition m) => !m.HasBody ? "NO_BODY" : string.Join("\n", m.Body.Instructions.Select(i => i.OpCode.Code + " " + Operand(i.Operand,m.Body))) +
    "\nLOCALS:" + string.Join(",",m.Body.Variables.Select(v=>v.VariableType.FullName)) + ":" + m.Body.InitLocals +
    "\nEXCEPTIONS:" + string.Join(";",m.Body.ExceptionHandlers.Select(e => e.HandlerType + ":" + e.CatchType?.FullName + ":" + Operand(e.TryStart,m.Body)+":"+Operand(e.TryEnd,m.Body)+":"+Operand(e.HandlerStart,m.Body)+":"+Operand(e.HandlerEnd,m.Body)+":"+Operand(e.FilterStart,m.Body)));
int unchanged = 0;
foreach (string file in new[] {"Assembly-CSharp.dll","Unity.RenderPipelines.Universal.Runtime.dll"}) {
    using var original = AssemblyDefinition.ReadAssembly(Path.Combine(managed,file+".NoDOF.original"));
    using var patched = AssemblyDefinition.ReadAssembly(Path.Combine(managed,file));
    var origMethods=Types(original.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
    var newMethods=Types(patched.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
    if (!origMethods.Keys.Order().SequenceEqual(newMethods.Keys.Order())) throw new Exception("Method set changed.");
    int changed=0;
    foreach(var pair in origMethods) {
        var after=newMethods[pair.Key];
        if(Body(pair.Value)==Body(after)) { unchanged++; continue; }
        changed++;
        if(file=="Assembly-CSharp.dll") {
            var expected = pair.Value.DeclaringType.FullName + "::" + pair.Value.Name;
            string hook = expected switch {
                "Biomes.Settings.SettingsManager::GetSettingsData" => "Register",
                "Biomes.Cam.CameraModuleZoom::get_ZoomOut" => "ExtendZoom",
                "Biomes.Cam.CameraModulePan::PitchMin" => "AdjustPitchMin",
                "Biomes.Cam.CameraModulePan::PitchMax" => "AdjustPitchMax",
                "Biomes.HexGrid.HexAreaBorderManager::MoveBorder" => "RefreshBorder",
                _ => throw new Exception("Unexpected changed method: "+pair.Key)
            };
            var a=pair.Value.Body.Instructions;var b=after.Body.Instructions;
            int extra=hook=="RefreshBorder" || hook.StartsWith("AdjustPitch")?3:2;
            if(b.Count!=a.Count+extra)throw new Exception("Unexpected hook size.");
            if(b[^(extra+1)].OpCode!=OpCodes.Ldarg_0 || b[^2].Operand is not MethodReference mr || !mr.FullName.Contains("BioEden.NoDOF.Runtime::"+hook) || b[^1].OpCode!=OpCodes.Ret)throw new Exception("Hook mismatch.");
            if(hook=="RefreshBorder" && (b[^3].OpCode!=OpCodes.Ldarg || ((ParameterDefinition)b[^3].Operand).Index!=3))throw new Exception("Wrong perimeter source parameter.");
            if(hook.StartsWith("AdjustPitch") && b[^3].OpCode!=OpCodes.Ldarg_1)throw new Exception("Wrong normalized zoom parameter.");
            for(int i=0;i<a.Count-1;i++) if(a[i].OpCode!=b[i].OpCode || Operand(a[i].Operand,pair.Value.Body)!=Operand(b[i].Operand,after.Body))throw new Exception("Settings body modified outside hook.");
        } else {
            if(pair.Value.Name!="IsActive" || pair.Value.DeclaringType.Name!="DepthOfField")throw new Exception("Unexpected renderer change.");
            var a=pair.Value.Body.Instructions;var b=after.Body.Instructions;
            if(b.Count!=a.Count+4 || b[0].OpCode!=OpCodes.Call || b[0].Operand is not MethodReference mr || !mr.FullName.Contains("BioEden.NoDOF.Runtime::get_Enabled") || b[1].OpCode!=OpCodes.Brtrue || b[1].Operand!=b[4] || b[2].OpCode!=OpCodes.Ldc_I4_0 || b[3].OpCode!=OpCodes.Ret)throw new Exception("Render gate mismatch.");
            // Remove the new prefix in-memory to normalize branch instruction indexes.
            for(int i=0;i<4;i++)b.RemoveAt(0);
            if(Body(pair.Value)!=Body(after))throw new Exception("Original DoF implementation was changed.");
        }
    }
    int expectedChanges=file=="Assembly-CSharp.dll"?5:1;
    if(changed!=expectedChanges)throw new Exception("Unexpected number of changed methods.");
    Console.WriteLine(file+$": exactly {expectedChanges} intended method(s) changed.");
}
Console.WriteLine($"PASS: {unchanged} other method bodies, locals and exception handlers unchanged. Original DoF path preserved exactly.");
