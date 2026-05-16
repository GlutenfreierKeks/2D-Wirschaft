using UnityEngine;
using System.Collections.Generic;

public class IslandManager : MonoBehaviour
{
    [Header("Island Settings")]
    [SerializeField] private Material islandMaterial;
    [SerializeField] private int islandCount = 5;
    [SerializeField] private int blocksPerIsland = 20;
    [SerializeField] private float mapRange = 500f;
    [SerializeField] private float blockSize = 1f;

    private void Start()
    {
        GenerateIslands();
    }

    private void GenerateIslands()
    {
        for (int i = 0; i < islandCount; i++)
        {
            // Random start position for the island cluster
            float startX = Mathf.Round(Random.Range(-mapRange, mapRange));
            float startY = Mathf.Round(Random.Range(-mapRange, mapRange));
            
            CreateIslandCluster(new Vector2(startX, startY));
        }
    }

    private void CreateIslandCluster(Vector2 startPos)
    {
        HashSet<Vector2> occupiedCells = new HashSet<Vector2>();
        occupiedCells.Add(startPos);
        
        List<Vector2> edgeCells = new List<Vector2> { startPos };

        for (int i = 0; i < blocksPerIsland; i++)
        {
            // Pick a random edge cell to grow from
            Vector2 current = edgeCells[Random.Range(0, edgeCells.Count)];
            
            // Random neighbor (Up, Down, Left, Right)
            Vector2[] neighbors = {
                current + Vector2.up,
                current + Vector2.down,
                current + Vector2.left,
                current + Vector2.right
            };
            
            Vector2 next = neighbors[Random.Range(0, neighbors.Length)];
            
            if (!occupiedCells.Contains(next))
            {
                occupiedCells.Add(next);
                edgeCells.Add(next);
                CreateBlock(next);
            }
        }
    }

    private void CreateBlock(Vector2 position)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
        block.name = "IslandBlock_" + position.ToString();
        block.transform.SetParent(transform);
        // Z is -0.1 to be in front of the grid (Z=0)
        block.transform.position = new Vector3(position.x, position.y, -0.1f);
        block.transform.localScale = new Vector3(blockSize, blockSize, 1);

        Destroy(block.GetComponent<MeshCollider>());

        if (islandMaterial != null)
        {
            block.GetComponent<Renderer>().material = islandMaterial;
        }
        else
        {
            block.GetComponent<Renderer>().material.color = Color.green;
        }
    }
}
