using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.StealthSample;

/// <summary>Coordinate-controlled AI checks, never ordinary-input acceptance.</summary>
public static class ExpandedAiChecks
{
    public static int PassedChecks { get; private set; }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("EXPANDED_AI_CHECK_FAIL: " + message);
    }
    static void Pass(string message) { PassedChecks++; Debug.Log("EXPANDED_AI_CHECK_PASS " + message); }

    public static IEnumerator Run()
    {
        Require(Application.isPlaying, "PlayMode required");
        var session = UnityEngine.Object.FindFirstObjectByType<ExpandedSession>();
        Require(session, "ExpandedSession present");
        // The aggregate runner must wait for Start and initialization before invoking us.
        session.enabled = false;
        var enemy = UnityEngine.Object.FindFirstObjectByType<ExpandedEnemy>();
        var player = UnityEngine.Object.FindFirstObjectByType<StealthPlayerController>();
        Require(enemy && player && player.controller && enemy.agent, "Runtime actors present");
        enemy.SetActive(false);
        var start = enemy.level.playerSpawn;
        Require(NavMesh.SamplePosition(start, out var hit, .5f, enemy.agent.areaMask), "Spawn has navigation for isolated logic fixture");
        start = hit.position;
        PassedChecks = 0;
        GameObject wall = null;
        try
        {
            Reset(enemy, player, start, 2);
            enemy.Tick(.1f);
            Require(enemy.State == ExpandedEnemyState.Alert && enemy.Awareness01 > 0, "First sight raises Alert, not instant Chase");
            enemy.Tick(1.0f);
            Require(enemy.State == ExpandedEnemyState.Alert && enemy.Awareness01 < 1, "Less than 1.2 seconds cannot complete Alert");
            float awareness = enemy.Awareness01;
            enemy.SetActive(false); enemy.Tick(4);
            Require(enemy.Awareness01 == awareness, "Pause freezes awareness");
            enemy.SetActive(true); enemy.Tick(.11f);
            Require(enemy.State == ExpandedEnemyState.Chase && enemy.Awareness01 == 1, "Alert completes into Chase after configured exposure");
            Pass("1.2-second warning window and pause freeze");

            Reset(enemy, player, start, 2);
            enemy.Tick(.6f);
            awareness = enemy.Awareness01;
            wall = Wall(start + Vector3.forward);
            enemy.Tick(.1f);
            Require(enemy.perception.Obstructed && enemy.Awareness01 < awareness, "Opaque cover reduces rather than accumulates awareness");
            enemy.Tick(1.5f);
            Require(enemy.State == ExpandedEnemyState.Patrol && enemy.Awareness01 == 0, "Occlusion clears incomplete alert");
            enemy.Tick(.5f);
            Require(enemy.Awareness01 == 0, "No through-wall awareness accumulation");
            UnityEngine.Object.DestroyImmediate(wall); wall = null; Physics.SyncTransforms();
            Pass("Occlusion and suspicion decay");

            Reset(enemy, player, start, .5f);
            enemy.agent.transform.rotation = Quaternion.Euler(0, 180, 0);
            enemy.Tick(.1f);
            Require(enemy.State == ExpandedEnemyState.Alert, "Close contact from behind still starts warning");
            enemy.Tick(1.11f);
            Require(enemy.State == ExpandedEnemyState.Chase, "Close contact cannot bypass full warning");
            enemy.Tick(.2f); enemy.Tick(.14f);
            Require(enemy.State == ExpandedEnemyState.Chase && enemy.CaptureElapsed < .35f, "Capture needs sustained confirmation");
            enemy.Tick(.02f);
            Require(enemy.State == ExpandedEnemyState.Caught && enemy.agent.isStopped, "Confirmed .35-second close capture is terminal");
            enemy.Tick(10);
            Require(enemy.State == ExpandedEnemyState.Caught, "Caught remains terminal");
            Pass("Close-contact warning and 0.35-second capture confirmation");

            Reset(enemy, player, start, 2);
            enemy.Tick(1.21f);
            Require(enemy.State == ExpandedEnemyState.Chase, "Search fixture enters Chase");
            Vector3 seen = enemy.LastSeen;
            wall = Wall(start + Vector3.forward);
            Place(player, start + Vector3.forward * 3);
            enemy.Tick(.1f);
            Require(enemy.State == ExpandedEnemyState.Search && enemy.LastSeen == seen, "Loss of sight enters Search with frozen last sighting");
            float searchBefore = enemy.SearchElapsed;
            enemy.SetActive(false); enemy.Tick(10);
            Require(enemy.SearchElapsed == searchBefore, "Pause freezes total search budget");
            enemy.SetActive(true);
            for (int i = 0; i < 13; i++)
            {
                enemy.Tick(.5f);
                Require(enemy.LastSeen == seen, "Hidden player position never refreshes search memory");
            }
            Require(enemy.State == ExpandedEnemyState.Return || enemy.State == ExpandedEnemyState.Patrol,
                "Total search duration includes travel and ends by six seconds");
            Pass("Frozen last sighting, paused timer and bounded search");
        }
        finally
        {
            if (wall) UnityEngine.Object.DestroyImmediate(wall);
            enemy.SetActive(false);
            player.SetActive(false);
        }
        // Yield once so fixture destruction is visible to the parent runner.
        yield return null;
        Debug.Log("EXPANDED_AI_LOGIC_PASS groups=" + PassedChecks + "; SCRIPTED FIXTURE ONLY");
    }

    static void Reset(ExpandedEnemy enemy, StealthPlayerController player, Vector3 start, float targetDistance)
    {
        enemy.SetActive(false);
        Require(enemy.agent.Warp(start), "Guard fixture position on NavMesh");
        enemy.agent.transform.rotation = Quaternion.identity;
        Place(player, start + Vector3.forward * targetDistance);
        enemy.Initialize();
        enemy.SetActive(true);
        Physics.SyncTransforms();
    }
    static void Place(StealthPlayerController player, Vector3 position)
    {
        player.controller.enabled = false;
        player.controller.transform.position = position;
        player.controller.enabled = true;
        Physics.SyncTransforms();
    }
    static GameObject Wall(Vector3 position)
    {
        var wall = new GameObject("ExpandedAiCheckTemporaryOccluder");
        wall.layer = 0;
        wall.transform.position = position + Vector3.up;
        wall.AddComponent<BoxCollider>().size = new Vector3(1.5f, 2, .15f);
        Physics.SyncTransforms();
        return wall;
    }
}
