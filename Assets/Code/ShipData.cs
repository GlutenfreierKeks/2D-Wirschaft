using UnityEngine;

public enum ShipType { Trade, Military }

[CreateAssetMenu(fileName = "NewShipData", menuName = "Ships/ShipData")]
public class ShipData : ScriptableObject
{
    [Header("Basic Info")]
    public string shipName = "Handelsschiff";
    public ShipType shipType = ShipType.Trade;
    public GameObject prefab;
    
    [Header("Size")]
    public int width = 1;
    public int height = 3;
    
    [Header("Capacity")]
    public int cargoCapacity = 10;  // How many items can be stored
    public int crewRequired = 1;    // Villagers needed to sail
    
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 180f;
    
    [Header("Visuals")]
    public Color shipColor = Color.white;
    public Sprite shipSprite;
}