using System;
using UnityEngine;

namespace LastCellOfPower.WarehouseExpanded
{
    [CreateAssetMenu(menuName = "Last Cell/Expanded Level Definition")]
    public sealed class ExpandedLevelDefinition : ScriptableObject
    {
        [Serializable] public struct PatrolStop
        {
            public Vector3 position;
            public float waitSeconds, lookYaw;
        }
        [Serializable] public struct CoverSpec
        {
            public string name;
            public Vector3 position, size;
            public CoverSpec(string name, float x, float z, float width, float height, float depth)
            { this.name = name; position = new Vector3(x, height * .5f, z); size = new Vector3(width, height, depth); }
        }
        public Vector2 minimum = new Vector2(-12, -12), maximum = new Vector2(12, 12);
        public Vector3 playerSpawn = new Vector3(-8, 0, -9), enemySpawn = new Vector3(7, 0, 7);
        public float enemyYaw = 0;
        public Vector3 terminal = new Vector3(-8, 0, 8), exitPosition = new Vector3(8, 0, 12.16f);
        public Vector3[] chargerPositions = { new Vector3(-9,0,-8), new Vector3(9,0,-6), new Vector3(-9,0,3) };
        public Vector3[] connectionCenters = { new Vector3(0,0,-7), new Vector3(0,0,7), new Vector3(-7,0,0), new Vector3(7,0,0) };
        public float connectionWidth = 3.2f, minimumMainAisleWidth = 2.4f;
        public PatrolStop[] patrolStops = DefaultPatrol();
        public CoverSpec[] coverSpecs = {
            new CoverSpec("SW_Observation", -7,-4,2,2,2), new CoverSpec("SW_ServiceStorage", -3,-3,2,1.5f,3),
            new CoverSpec("SE_ShelfLoopA",4,-4,2,2.2f,4.4f), new CoverSpec("SE_ShelfLoopB",8,-3,3.2f,2,1.2f),
            new CoverSpec("SE_CrateLoop",8.8f,-9,3,1.6f,2), new CoverSpec("NW_EquipmentCover",-5,5,2,2.2f,2),
            new CoverSpec("NW_NorthCrates",-3,9.4f,2,2,2), new CoverSpec("NE_LoadingCover",4,4,3,2,3),
            new CoverSpec("NE_CrateStack",9,8.6f,2,1.8f,2)
        };

        static PatrolStop[] DefaultPatrol()
        {
            Vector3[] positions = { new Vector3(7,0,7), new Vector3(7,0,3), new Vector3(7,0,-1.5f),
                new Vector3(10,0,-5), new Vector3(6,0,-10), new Vector3(3,0,-7), new Vector3(7,0,-1.5f),
                new Vector3(7,0,3), new Vector3(7,0,7), new Vector3(3,0,7), new Vector3(-3,0,7),
                new Vector3(-4,0,3), new Vector3(-3,0,7), new Vector3(3,0,7) };
            var stops = new PatrolStop[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var delta = positions[(i + 1) % positions.Length] - positions[i];
                stops[i] = new PatrolStop { position = positions[i], waitSeconds = .5f, lookYaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg };
            }
            return stops;
        }
        public string ZoneName(Vector3 point)
        {
            if (point.x < minimum.x || point.x > maximum.x || point.z < minimum.y || point.z > maximum.y) return "出口平台";
            if (point.z < 0) return point.x < 0 ? "维修起步区" : "货架区";
            return point.x < 0 ? "设备区" : "装卸出口区";
        }
        public void Validate()
        {
            if (maximum.x <= minimum.x || maximum.y <= minimum.y || connectionWidth < minimumMainAisleWidth || minimumMainAisleWidth <= 0 ||
                chargerPositions == null || chargerPositions.Length < 1 || patrolStops == null || patrolStops.Length < 2 || coverSpecs == null || connectionCenters == null || connectionCenters.Length != 4)
                throw new InvalidOperationException("Invalid expanded level definition");
            foreach (var stop in patrolStops)
            {
                if (stop.waitSeconds < 0 || float.IsNaN(stop.lookYaw)) throw new InvalidOperationException("Invalid patrol stop");
                if (stop.position.x < 0 && stop.position.z < 0) throw new InvalidOperationException("Routine patrol may not enter the starting quadrant");
                if (Vector3.Distance(stop.position, terminal) < 4) throw new InvalidOperationException("Patrol stop is too close to terminal");
            }
            foreach (var cover in coverSpecs)
                if (cover.size.x <= 0 || cover.size.y <= 0 || cover.size.z <= 0) throw new InvalidOperationException("Invalid cover dimensions");
        }
    }
}
