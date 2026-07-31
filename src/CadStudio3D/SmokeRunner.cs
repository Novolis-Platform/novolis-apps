using Novolis.Agent.Core;
using Novolis.Avalonia._3D.Session;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Cad.SceneBridge;
using Novolis.Modeling.Scene;

namespace CadStudio3D;

/// <summary>Agent-first smoke: Cad Execute → bridge → Scene Execute (no UI).</summary>
public static class SmokeRunner
{
    public static int Run()
    {
        var failures = 0;

        void Check(string name, bool ok, string detail = "")
        {
            if (ok)
            {
                Console.WriteLine($"  OK  {name}");
                return;
            }

            failures++;
            Console.WriteLine($"  FAIL {name}{(string.IsNullOrEmpty(detail) ? "" : ": " + detail)}");
        }

        Console.WriteLine("Novolis CAD Studio 3D smoke (agent-first)");

        var root = Path.Combine(Path.GetTempPath(), "novolis-cadstudio3d-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new CadEditorSettings(root);
            var document = new CadDocumentSession(settings);
            var bus = new CadCommandBus(document);
            var dispatcher = new CadCommandDispatcher(document, bus, settings);
            var cad = new CadSessionService(document, settings, bus, dispatcher)
            {
                AppId = "cad-studio-3d-smoke",
                AppTitle = "Novolis CAD Studio 3D",
            };
            var scene = new SceneSessionService { AppId = "cad-studio-3d-scene-smoke" };

            cad.SceneBridged += doc => scene.ReplaceDocument(doc);

            Check("cad actions exportscene", cad.Actions().Actions.Any(a => a.Id == CadSessionActionIds.ExportScene));
            Check("cad actions bridgescene", cad.Actions().Actions.Any(a => a.Id == CadSessionActionIds.BridgeScene));
            Check("cad actions setstudioworkspace", cad.Actions().Actions.Any(a => a.Id == CadSessionActionIds.SetStudioWorkspace));
            Check("scene actions setmeshmaterial", scene.Actions().Actions.Any(a => a.Id == SceneSessionActionIds.SetMeshMaterial));
            Check("scene actions ensurestudiolights", scene.Actions().Actions.Any(a => a.Id == SceneSessionActionIds.EnsureStudioLights));
            Check("scene actions saverenderpng", scene.Actions().Actions.Any(a => a.Id == SceneSessionActionIds.SaveRenderPng));

            var n = cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.New });
            Check("cad new", n.Ok, n.Message);

            var ws = cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetStudioWorkspace,
                Workspace = "draft2d",
            });
            Check("cad setstudioworkspace", ws.Ok, ws.Message);

            var rect = cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.AddRect,
                Properties = new Dictionary<string, string>
                {
                    ["a"] = "0,0,0",
                    ["b"] = "4,0,3",
                },
            });
            Check("cad addrect", rect.Ok, rect.Message);

            var extrude = cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.ExtrudeProfile,
                Properties = new Dictionary<string, string>
                {
                    ["points"] = "0,0,0;4,0,0;4,0,3;0,0,3",
                    ["height"] = "2.4",
                },
            });
            Check("cad extrudeprofile", extrude.Ok, extrude.Message);

            var mat = cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.SetMaterial,
                Kind = "Concrete",
            });
            Check("cad setmaterial", mat.Ok, mat.Message);

            var scenePath = Path.Combine(root, "smoke.nov3djson");
            var export = cad.Execute(new CadCommandDto
            {
                ActionId = CadSessionActionIds.ExportScene,
                Path = scenePath,
            });
            Check("cad exportscene", export.Ok, export.Message);
            Check("nov3djson exists", File.Exists(scenePath));
            Check("nov3djson bytes", File.Exists(scenePath) && new FileInfo(scenePath).Length > 100);

            var bridge = cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.BridgeScene });
            Check("cad bridgescene", bridge.Ok, bridge.Message);
            Check("scene has meshes", scene.Document.Nodes.OfType<MeshNode>().Any());

            var lights = scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.EnsureStudioLights });
            Check("scene ensurestudiolights", lights.Ok, lights.Message);

            var describe = scene.Execute(new AgentCommand { ActionId = SceneSessionActionIds.DescribeScene });
            Check("scene describescene", describe.Ok, describe.Message);

            // Round-trip load exported file
            var reloaded = SceneSerializer.Load(scenePath);
            Check("reload scene nodes", reloaded.Nodes.Count >= 1);

            // Library bridge direct path
            var direct = CadSceneBridge.ToSceneDocument(document.Document);
            Check("bridge library meshes", direct.Nodes.OfType<MeshNode>().Any());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* best effort */ }
        }

        Console.WriteLine(failures == 0 ? "SMOKE_OK" : $"SMOKE_FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }
}
