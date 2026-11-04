using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class EvolvingShip : MonoBehaviour
{
    [Header("Sensores")]
    [SerializeField] private float sensorDistance = 8f;
    [SerializeField] private float sensorAngle = 35f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Movimento")]
    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float turnSpeed = 180f;

    private readonly float[] inputs = new float[NeuralNetwork.InputCount];
    private readonly NeuralNetwork brain = new();
    private Rigidbody body;
    private EvolutionManager manager;
    private Genome genome;
    private Transform goal;
    private float initialDistance;
    private bool active;
    private Vector3 lastPosition;
    private float episodeTime;
    private float realDistance;

    public float Fitness { get; private set; }
    public Genome Genome => genome;
    public bool IsActive => active;
    public LayerMask ObstacleMask => obstacleMask;
    public EpisodeMetrics Metrics { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void Begin(EvolutionManager owner, Genome newGenome, Transform newGoal, Vector3 position, Quaternion rotation)
    {
        manager = owner;
        genome = newGenome;
        goal = newGoal;
        transform.SetPositionAndRotation(position, rotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        initialDistance = Vector3.Distance(transform.position, goal.position);
        Fitness = 0f;
        lastPosition = transform.position;
        episodeTime = 0f;
        realDistance = 0f;
        active = true;
        gameObject.SetActive(true);
    }

    // Interrompe o episodio sem reportar ao manager (usado ao trocar de modo).
    public void Abort()
    {
        active = false;
        body.linearVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (!active)
            return;

        AccumulateDistance();
        episodeTime += Time.fixedDeltaTime;

        ReadSensors();
        brain.Forward(inputs, genome.Weights);

        float turn = brain.GetOutput(0);
        float throttle = Mathf.Clamp01((brain.GetOutput(1) + 1f) * 0.5f);
        Quaternion delta = Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        body.MoveRotation(body.rotation * delta);
        body.linearVelocity = transform.forward * (throttle * maxSpeed);

        // Progresso continuo evita que sobreviver parado seja uma boa estrategia.
        Fitness = (initialDistance - Vector3.Distance(transform.position, goal.position)) * manager.ProgressWeight;
    }

    private void ReadSensors()
    {
        inputs[0] = ReadSensor(transform.forward);
        inputs[1] = ReadSensor(Quaternion.AngleAxis(-sensorAngle, Vector3.up) * transform.forward);
        inputs[2] = ReadSensor(Quaternion.AngleAxis(sensorAngle, Vector3.up) * transform.forward);
    }

    private float ReadSensor(Vector3 direction)
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, sensorDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.distance / sensorDistance;

        return 1f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (active && ((1 << collision.gameObject.layer) & obstacleMask.value) != 0)
            Finish(manager.CollisionPenalty, false, true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (active && other.transform == goal)
            Finish(manager.GoalBonus, true, false);
    }

    public void Timeout()
    {
        if (active)
            Finish(-manager.TimePenalty, false, false);
    }

    // Distancia real = soma dos deslocamentos entre um passo de fisica e o seguinte.
    private void AccumulateDistance()
    {
        Vector3 position = transform.position;
        realDistance += Vector3.Distance(position, lastPosition);
        lastPosition = position;
    }

    private void Finish(float bonusOrPenalty, bool reachedGoal, bool collided)
    {
        Fitness += bonusOrPenalty;
        AccumulateDistance(); // inclui o ultimo deslocamento antes do evento.
        Metrics = new EpisodeMetrics(reachedGoal, collided, episodeTime, realDistance, manager.ReferenceDistance);
        active = false;
        body.linearVelocity = Vector3.zero;
        manager.ReportFinished(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        Gizmos.DrawRay(origin, transform.forward * sensorDistance);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(-sensorAngle, Vector3.up) * transform.forward * sensorDistance);
        Gizmos.DrawRay(origin, Quaternion.AngleAxis(sensorAngle, Vector3.up) * transform.forward * sensorDistance);
    }
}
