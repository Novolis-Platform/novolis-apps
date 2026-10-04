using Novolis.Agent.Core;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Cad.SceneBridge;
using Novolis.Ship.Design;
using Novolis.ThreeD;

namespace CadStudio;

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

        Console.WriteLine("Novolis CAD Studio smoke (agent-first)");

        var root = Path.Combine(Path.GetTempPath(), "novolis-cadstudio-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new CadEditorSettings(root);
            var document = new CadDocumentSession(settings);
            var bus = new CadCommandBus(document);
            var dispatcher = new CadCommandDispatcher(document, bus, settings);
            var cad = new CadSessionService(document, settings, bus, dispatcher)
            {
                AppId = "cad-studio-smoke",
                AppTitle = "Novolis CAD Studio",
            };
            var scene = new SceneSessionService { AppId = "cad-studio-scene-smoke" };
            var shipCadHost = new ShipCadSession(root);
            var shipCad = shipCadHost.Service;

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

            var productEntityCount = document.Document.Entities.Count;

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

            var reloaded = SceneSerializer.Load(scenePath);
            Check("reload scene nodes", reloaded.Nodes.Count >= 1);

            var direct = CadSceneBridge.ToSceneDocument(document.Document);
            Check("bridge library meshes", direct.Nodes.OfType<MeshNode>().Any());

            Check("generic ship hooks absent", cad.ExteriorHooks is null);
            Check("generic ship import absent", !cad.Actions().Actions.Any(a => a.Id == "importship"));

            var ship = new ShipDesignSession(shipCadHost.Settings.DataRoot);
            using var shipAttachment = ShipDesignChrome.Attach(shipCad, ship);
            Check("ship import action scoped", shipCad.Actions().Actions.Any(
                a => a.Id == "importship"));
            Check("ship validation action scoped", shipCad.Actions().Actions.Any(
                a => a.Id == "validateship"));
            Check("product cad has no ship import", !cad.Actions().Actions.Any(
                a => a.Id == "importship"));
            Check("product cad has no exterior hooks", cad.ExteriorHooks is null);

            ship.NewShip(ShipDesignSession.DefaultDefinition("Smoke CAD Ship"));
            Check("ship mode creates design", ship.HasShip);
            Check("ship mode seeds hull", ship.Design.Hull.Geometry.Entities.Count > 0);
            ship.SetWorkspace(ShipWorkspaceKind.Model);
            ship.Select(ship.Design.Hull.Id.AsObject());
            ship.Notify();
            Check("ship mode projects model", shipCad.Document.Document.Entities.Count > 0);
            Check(
                "product cad entities unchanged",
                document.Document.Entities.Count == productEntityCount);

            var shipPath = Path.Combine(shipCadHost.Settings.DataRoot, "smoke.shipjson");
            ship.SaveTo(shipPath);
            Check("ship mode saves shipjson", File.Exists(shipPath));
            ship.OpenFromPath(shipPath);
            Check("ship mode reloads shipjson", !ship.IsDirty && ship.HasShip);

            shipAttachment.Dispose();
            Check("ship hooks detach", shipCad.ExteriorHooks is null);
            Check("ship actions detach", !shipCad.Actions().Actions.Any(
                a => a.Id == "importship"));
            Check(
                "product cad still unchanged after detach",
                document.Document.Entities.Count == productEntityCount);
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
