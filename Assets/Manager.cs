using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Manager : MonoBehaviour
{
    public static Manager instance { get; private set; }
    public GameObject box;
    public GameObject barrel;
    public GameObject turret;
    public GameObject enemy;
    public Vector3 screenSize;
    public Vector3 objectSize;
    public GameObject navMesh;

    private float scale = 0.2f;
    private float noise = 0.5f;

    private int barrelAmount = 3;

    private float currentOffset;
    public string state = "Idle";

    public int maxTimer = 5;
    public float waveTimer;
    public int currentWave = 0;
    public int currentPower = 0;

    private Dictionary<string, int> objectDict = new Dictionary<string, int>
    {
        { "Turret", 0 },
        { "Barrel", 1 },
        { "Enemy", 2 },
        { "Box", 3 }
    };

    private Dictionary<string, int> powerDict = new Dictionary<string, int>
    {
        { "Turret", 100 },
        { "Enemy", 10 }
    };

    private Dictionary<string, int> amountDict = new Dictionary<string, int>
    {
        { "Turret", 0 },
        { "Enemy", 0 }
    };

    private List<string> enemies = new List<string> { "Turret", "Enemy" };

    private List<Vector2Int> objectSpots = new List<Vector2Int>();
    private List<Vector2Int> enemySpots = new List<Vector2Int>();

    private List<List<GameObject>> objectLists = new List<List<GameObject>>();

    // Start is called before the first frame update

    void Awake()
    {
        if (instance != null && instance != this)
            Destroy(this.gameObject);
        else
            instance = this;
    }

    void Start()
    {
        screenSize = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));
        objectSize = barrel.transform.localScale;

        for (int i = 0; i < objectDict.Count; i++)
            objectLists.Add(new List<GameObject>());

        NewMap();
        waveTimer = maxTimer;
        state = "Generating";
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (state == "Idle")
            waveTimer -= Time.fixedDeltaTime;

        if (Input.GetMouseButtonDown(1) || waveTimer < 0)
        {
            waveTimer = maxTimer;
            ClearMap();
            state = "Clearing";
        }
    }

    void NewMap()
    {
        currentOffset = Random.Range(100, 10000);
        List<List<float>> list = GenerateMap();
        AddEnemies();
        StartCoroutine(LoadMap(list));
    }

    void ClearMap()
    {
        for (int i = 0; i < objectLists.Count; i++)
            objectLists[i].RemoveAll(s => s == null);
        currentWave++;
        for (int i = 0; i < AIManager.instance.spots; i++)
            AIManager.instance.dict[i] = true;
        StartCoroutine(DestroyObjects());
    }

    void AddEnemies()
    {
        currentPower += 50; //* currentWave  + 150;
        List<string> spawns = new List<string>();

        //get strongest enemy spawn
        (string, int) strongestSpawn = ("_", 0);
        foreach (string enemy in powerDict.Keys)
            if (powerDict[enemy] > strongestSpawn.Item2 && powerDict[enemy] <= currentPower)
            {
                strongestSpawn.Item1 = enemy;
                strongestSpawn.Item2 = powerDict[enemy];
            }
        spawns.Add(strongestSpawn.Item1);
        currentPower -= strongestSpawn.Item2;

        //get random enemies
        while (currentPower > 0)
        {
            int randomIndex = Random.Range(0, enemies.Count);
            string randomKey = enemies[randomIndex];
            if (currentPower >= powerDict[randomKey])
            {
                currentPower -= powerDict[randomKey];
                spawns.Add(randomKey);
            }
        }

        //add spawns to spawning list
        for (int i = 0; i < spawns.Count; i++)
            amountDict[spawns[i]]++;

        //add any leftover enemies from last wave
        List<string> keyList = new List<string>(amountDict.Keys);
        foreach (string enemy in keyList)
            amountDict[enemy] += objectLists[objectDict[enemy]].Count;
        for (int i = 0; i < objectLists.Count; i++)
            objectLists[i].Clear();
    }

    IEnumerator DestroyObjects()
    {
        WaitForSeconds wait = new WaitForSeconds(0.005f);
        foreach (List<GameObject> objectList in objectLists)
            for (int i = 0; i < objectList.Count; i++)
            {
                if (objectList[i] == null)
                    continue;
                objectList[i].SendMessage("Death");
                yield return wait;
            }
        yield return wait;
        state = "Generating";
        objectSpots.Clear();
        NewMap();
    }

    IEnumerator LoadMap(List<List<float>> list)
    {
        WaitForSeconds wait = new WaitForSeconds(0.005f);

        //boxes
        Vector2Int middle = new Vector2Int(list[0].Count / 2, list.Count / 2);
        for (int y = 0; y < list.Count; y++)
            for (int x = 0; x < list[y].Count; x++)
            {
                Vector3 pos = new Vector3(
                    (x + 0.5f) * objectSize.x - screenSize.x,
                    (y + 0.5f) * objectSize.y - screenSize.y,
                    0
                );
                if (list[y][x] > noise)
                {
                    if (Mathf.Abs(middle.x - x) > 1 && Mathf.Abs(middle.y - y) > 1)
                    {
                        GameObject spawned = Instantiate(box, pos, box.transform.rotation);
                        objectLists[objectDict[spawned.tag]].Add(spawned);
                        spawned.transform.parent = navMesh.transform;
                        yield return wait;
                    }
                }
                else if (!Neighbours(list, x, y))
                    objectSpots.Add(new Vector2Int(x, y));
                else
                    enemySpots.Add(new Vector2Int(x, y));
            }

        //enemies
        enemySpots.Shuffle();
        for (int i = amountDict["Enemy"]; i > 0; i--)
        {
            Vector3 pos = new Vector3(
                (enemySpots[i].x + 0.5f) * objectSize.x - screenSize.x,
                (enemySpots[i].y + 0.5f) * objectSize.y - screenSize.y,
                0
            );
            GameObject spawned = Instantiate(enemy, pos, enemy.transform.rotation);
            amountDict["Enemy"]--;
            objectLists[objectDict[spawned.tag]].Add(spawned);
            yield return wait;
        }

        List<Vector2Int> spots = new List<Vector2Int>();
        List<int> randomNums = GenerateRandom(
            barrelAmount + amountDict["Turret"],
            0,
            objectSpots.Count
        );
        foreach (int num in randomNums)
        {
            int crashFix = 0;
            int test = num;
            while (Neighbours2(spots, objectSpots[test]))
            {
                test = Random.Range(0, objectSpots.Count);
                crashFix += 1;
                if (crashFix > 100)
                {
                    this.Log("Crash fix1!");
                    break;
                }
            }
            spots.Add(objectSpots[test]);
        }

        //objects
        for (int i = 0; i < spots.Count; i++)
        {
            GameObject spawned;
            Vector3 pos = new Vector3(
                (spots[i].x + 0.5f) * objectSize.x - screenSize.x,
                (spots[i].y + 0.5f) * objectSize.y - screenSize.y,
                0
            );
            if (i < amountDict["Turret"])
            {
                spawned = Instantiate(turret, pos, turret.transform.rotation);
                spawned.transform.parent = navMesh.transform;
            }
            else
                spawned = Instantiate(barrel, pos, barrel.transform.rotation);
            spawned.transform.parent = navMesh.transform;
            objectLists[objectDict[spawned.tag]].Add(spawned);
            yield return wait;
        }

        yield return wait;
        state = "Idle";
    }

    //function to generate a perlin noise map
    List<List<float>> GenerateMap()
    {
        List<List<float>> list = new List<List<float>>();
        for (int y = 0; y < 1080 / 60; y++)
        {
            list.Add(new List<float>());
            for (int x = 0; x < 1920 / 60; x++)
            {
                list[y].Add(Mathf.PerlinNoise(x * scale + currentOffset, y * scale + 30));
            }
        }
        return list;
    }

    // get an amount of random numbers between min and max
    List<int> GenerateRandom(int count, int min, int max)
    {
        List<int> list = new List<int>();
        int random;
        for (int i = 0; i < count; i++)
        {
            int crashFix = 0;
            random = Random.Range(min, max);
            while (list.Contains(random))
            {
                random = Random.Range(min, max);
                crashFix += 1;
                if (crashFix > 100)
                {
                    this.Log("Crash fix2!");
                    break;
                }
            }
            list.Add(random);
        }
        return list;
    }

    public bool InScreen(Vector3 pos)
    {
        if (Mathf.Abs(pos.x) < screenSize.x && Mathf.Abs(pos.y) < screenSize.y)
            return true;
        else
            return false;
    }

    bool Neighbours(List<List<float>> list, int x, int y)
    {
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                if (
                    y + i >= 0
                    && y + i < list.Count
                    && x + j >= 0
                    && x + j < list[y].Count
                    && list[y + i][x + j] > noise
                )
                    return true;
        return false;
    }

    bool Neighbours2(List<Vector2Int> list, Vector2Int pos)
    {
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                if (list.Contains(new Vector2Int(pos.x + i, pos.y + j)))
                    return true;
        return false;
    }
}
