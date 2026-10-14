using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class WeaponKeyring : MonoBehaviour
    {
        private const int ChainLinkCount = 6;
        private const float ChainLinkPitch = 0.0211f;
        private const float ChainLinkLength = 0.0253f;
        private const float ChainLinkWidth = 0.009f;
        private const float ChainLinkWireRadius = 0.0012f;
        private const int ChainPathSegments = 32;
        private const int ChainTubeSegments = 10;
        private const float ModelColliderRadius = 0.062f / 1.3f;
        private const float StandardModelSize = 0.11f;
        private const float MountSurfaceOffset = 0.009f;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private WeaponKeyringHistory history;
        private EquipableWeapon weapon;
        private Transform attachmentFrame;
        private Vector3 attachmentLocalPoint;
        private Rigidbody[] chainBodies;
        private Collider[] chainColliders;
        private Rigidbody modelBody;
        private Collider modelCollider;
        private ConfigurableJoint mountJoint;
        private ConfigurableJoint modelJoint;
        private Vector3 lastWorldMountAnchor;
        private bool hasWorldMountAnchor;
        private Mesh chainLinkMesh;
        private Material chainMaterial;

        public bool Initialize(WeaponKeyringHistory owner, EquipableWeapon owningWeapon,
            GameObject modelPrefab, Transform surfaceFrame, Vector3 worldPoint, Vector3 worldNormal)
        {
            if (owner == null || owningWeapon == null || modelPrefab == null || surfaceFrame == null ||
                (surfaceFrame != owningWeapon.transform && !surfaceFrame.IsChildOf(owningWeapon.transform)))
                return false;

            history = owner;
            weapon = owningWeapon;
            attachmentFrame = surfaceFrame;
            Vector3 normal = worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : surfaceFrame.up;
            attachmentLocalPoint = surfaceFrame.InverseTransformPoint(worldPoint + normal * MountSurfaceOffset);
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            BuildPhysicalChain();
            lastWorldMountAnchor = GetMountPoint();
            hasWorldMountAnchor = true;
            float modelHalfHeight = BuildHangingModel(modelPrefab);
            ConnectChainAndModel(modelHalfHeight);
            IgnoreCollisionsWithWeapon();
            return true;
        }

        private void BuildPhysicalChain()
        {
            chainBodies = new Rigidbody[ChainLinkCount];
            chainColliders = new Collider[ChainLinkCount];
            chainLinkMesh = CreateChainLinkMesh();
            chainMaterial = CreateChainMaterial();
            Vector3 mountPoint = GetMountPoint();
            Vector3 gravityDirection = GetGravityDirection();
            Quaternion chainRotation = Quaternion.FromToRotation(Vector3.up, -gravityDirection);
            Rigidbody previousBody = null;
            Rigidbody weaponBody = weapon.GetComponent<Rigidbody>();
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

            for (int i = 0; i < ChainLinkCount; i++)
            {
                GameObject link = new GameObject("Silver Chain Link " + (i + 1));
                link.transform.SetParent(transform, true);
                Quaternion linkRotation = i % 2 == 0
                    ? chainRotation
                    : Quaternion.AngleAxis(90f, -gravityDirection) * chainRotation;
                link.transform.SetPositionAndRotation(
                    mountPoint + gravityDirection * (ChainLinkPitch * (i + 0.5f)), linkRotation);
                if (ignoreRaycastLayer >= 0) link.layer = ignoreRaycastLayer;

                MeshFilter meshFilter = link.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = chainLinkMesh;
                MeshRenderer meshRenderer = link.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = chainMaterial;

                CapsuleCollider shape = link.AddComponent<CapsuleCollider>();
                shape.direction = 1;
                shape.radius = ChainLinkWireRadius * 1.5f;
                shape.height = ChainLinkLength;
                Rigidbody body = link.AddComponent<Rigidbody>();
                body.mass = 0.008f;
                body.useGravity = true;
                body.linearDamping = 0.7f;
                body.angularDamping = 1.1f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.solverIterations = 40;
                body.solverVelocityIterations = 32;

                ConfigurableJoint joint = link.AddComponent<ConfigurableJoint>();
                Vector3 connectedAnchor;
                if (previousBody != null)
                {
                    joint.connectedBody = previousBody;
                    connectedAnchor = Vector3.down * (ChainLinkPitch * 0.5f);
                }
                else
                {
                    joint.connectedBody = weaponBody;
                    connectedAnchor = weaponBody != null
                        ? weaponBody.transform.InverseTransformPoint(mountPoint)
                        : mountPoint;
                    mountJoint = joint;
                }

                ConfigureChainJoint(joint, Vector3.up * (ChainLinkPitch * 0.5f), connectedAnchor);
                chainBodies[i] = body;
                chainColliders[i] = shape;
                previousBody = body;
            }
        }

        private float BuildHangingModel(GameObject modelPrefab)
        {
            Vector3 gravityDirection = GetGravityDirection();
            Vector3 mountPoint = GetMountPoint();
            Quaternion modelRotation = Quaternion.FromToRotation(Vector3.up, -gravityDirection);
            Vector3 chainEndpoint = mountPoint + gravityDirection * (ChainLinkCount * ChainLinkPitch);

            GameObject modelBodyObject = new GameObject(modelPrefab.name + " Keyring Model");
            modelBodyObject.transform.SetParent(transform, true);
            modelBodyObject.transform.SetPositionAndRotation(chainEndpoint, modelRotation);
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0) modelBodyObject.layer = ignoreRaycastLayer;

            GameObject model = Instantiate(modelPrefab, modelBodyObject.transform, false);
            model.name = modelPrefab.name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            ApplyMatchingArtwork(model);
            SetLayerRecursively(model.transform, ignoreRaycastLayer);
            foreach (Collider visualCollider in model.GetComponentsInChildren<Collider>(true))
            {
                visualCollider.enabled = false;
                Destroy(visualCollider);
            }
            float modelHalfHeight = NormalizeModel(model, modelBodyObject.transform);
            modelBodyObject.transform.position = chainEndpoint + gravityDirection * modelHalfHeight;

            modelBody = modelBodyObject.AddComponent<Rigidbody>();
            modelBody.mass = 0.09f;
            modelBody.useGravity = true;
            modelBody.linearDamping = 0.8f;
            modelBody.angularDamping = 1.2f;
            modelBody.interpolation = RigidbodyInterpolation.Interpolate;
            modelBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            modelBody.solverIterations = 40;
            modelBody.solverVelocityIterations = 32;

            SphereCollider sphere = modelBodyObject.AddComponent<SphereCollider>();
            sphere.radius = ModelColliderRadius;
            modelCollider = sphere;
            return modelHalfHeight;
        }

        private static void ApplyMatchingArtwork(GameObject model)
        {
            if (model == null) return;
            Texture2D artwork = Resources.Load<Texture2D>("Keyrings/" + model.name);
            if (artwork == null) return;

            var properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;

                    bool hasArtworkSlot = material.HasProperty(BaseMapId) || material.HasProperty(MainTexId);
                    if (!hasArtworkSlot) continue;

                    renderer.GetPropertyBlock(properties, materialIndex);
                    if (material.HasProperty(BaseMapId)) properties.SetTexture(BaseMapId, artwork);
                    if (material.HasProperty(MainTexId)) properties.SetTexture(MainTexId, artwork);
                    if (material.HasProperty(BaseColorId)) properties.SetColor(BaseColorId, Color.white);
                    if (material.HasProperty(ColorId)) properties.SetColor(ColorId, Color.white);
                    renderer.SetPropertyBlock(properties, materialIndex);
                }
            }
        }

        private static float NormalizeModel(GameObject model, Transform bodyFrame)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                Bounds rendererBounds = GetModelRendererBounds(renderer);
                Vector3 min = rendererBounds.min;
                Vector3 max = rendererBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localPoint = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 point = bodyFrame.InverseTransformPoint(renderer.transform.TransformPoint(localPoint));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!hasBounds) return StandardModelSize * 0.5f;

            float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largestDimension <= 0.0001f) return StandardModelSize * 0.5f;

            float scaleFactor = StandardModelSize / largestDimension;
            model.transform.localScale *= scaleFactor;
            model.transform.localPosition = -bounds.center * scaleFactor;
            return bounds.extents.y * scaleFactor;
        }

        private static Bounds GetModelRendererBounds(Renderer renderer)
        {
            // Skinned renderer bounds can include imported animation ranges instead of the mesh's own size.
            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
                return skinnedRenderer.sharedMesh.bounds;

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
                return meshFilter.sharedMesh.bounds;

            return renderer.localBounds;
        }

        private void ConnectChainAndModel(float modelHalfHeight)
        {
            modelJoint = modelBody.gameObject.AddComponent<ConfigurableJoint>();
            modelJoint.connectedBody = chainBodies[ChainLinkCount - 1];
            ConfigureChainJoint(modelJoint, Vector3.up * modelHalfHeight,
                Vector3.down * (ChainLinkPitch * 0.5f));
        }

        private static void ConfigureChainJoint(ConfigurableJoint joint, Vector3 localAnchor,
            Vector3 connectedAnchor)
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = localAnchor;
            joint.connectedAnchor = connectedAnchor;
            // Lock translation so the chain keeps its length while each link can rotate freely.
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.rotationDriveMode = RotationDriveMode.XYAndZ;
            JointDrive angularDamping = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = 0.5f,
                maximumForce = 2f
            };
            joint.angularXDrive = angularDamping;
            joint.angularYZDrive = angularDamping;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.002f;
            joint.projectionAngle = 8f;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }

        private void IgnoreCollisionsWithWeapon()
        {
            Collider[] weaponColliders = weapon.GetComponentsInChildren<Collider>(true);
            foreach (Collider chainCollider in chainColliders)
            {
                if (chainCollider == null) continue;
                foreach (Collider weaponCollider in weaponColliders)
                    if (weaponCollider != null) Physics.IgnoreCollision(chainCollider, weaponCollider, true);
                Physics.IgnoreCollision(chainCollider, modelCollider, true);
            }

            for (int i = 0; i < chainColliders.Length; i++)
                for (int j = i + 1; j < chainColliders.Length; j++)
                    Physics.IgnoreCollision(chainColliders[i], chainColliders[j], true);

            foreach (Collider weaponCollider in weaponColliders)
                if (weaponCollider != null) Physics.IgnoreCollision(modelCollider, weaponCollider, true);
        }

        private static Mesh CreateChainLinkMesh()
        {
            int vertexCount = ChainPathSegments * ChainTubeSegments;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[ChainPathSegments * ChainTubeSegments * 6];
            float centerlineHalfLength = ChainLinkLength * 0.5f - ChainLinkWireRadius;
            float centerlineHalfWidth = ChainLinkWidth * 0.5f - ChainLinkWireRadius;

            for (int path = 0; path < ChainPathSegments; path++)
            {
                float angle = path * Mathf.PI * 2f / ChainPathSegments;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                Vector3 center = new Vector3(centerlineHalfWidth * cosine,
                    centerlineHalfLength * sine, 0f);
                Vector3 outward = new Vector3(cosine / centerlineHalfWidth,
                    sine / centerlineHalfLength, 0f).normalized;

                for (int tube = 0; tube < ChainTubeSegments; tube++)
                {
                    float tubeAngle = tube * Mathf.PI * 2f / ChainTubeSegments;
                    Vector3 ringOffset = outward * Mathf.Cos(tubeAngle) +
                                         Vector3.forward * Mathf.Sin(tubeAngle);
                    vertices[path * ChainTubeSegments + tube] = center + ringOffset * ChainLinkWireRadius;
                }
            }

            int triangle = 0;
            for (int path = 0; path < ChainPathSegments; path++)
                for (int tube = 0; tube < ChainTubeSegments; tube++)
                {
                    int nextPath = (path + 1) % ChainPathSegments;
                    int nextTube = (tube + 1) % ChainTubeSegments;
                    int a = path * ChainTubeSegments + tube;
                    int b = nextPath * ChainTubeSegments + tube;
                    int c = path * ChainTubeSegments + nextTube;
                    int d = nextPath * ChainTubeSegments + nextTube;
                    triangles[triangle++] = a;
                    triangles[triangle++] = b;
                    triangles[triangle++] = c;
                    triangles[triangle++] = b;
                    triangles[triangle++] = d;
                    triangles[triangle++] = c;
                }

            var mesh = new Mesh { name = "Silver Keyring Chain Link" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateChainMaterial()
        {
            Material material = CreateMaterial(new Color(0.68f, 0.71f, 0.77f), false);
            if (material != null && material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.82f);
            if (material != null && material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.78f);
            if (material != null && material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.78f);
            return material;
        }

        private void FixedUpdate()
        {
            if (weapon == null || attachmentFrame == null)
            {
                Destroy(gameObject);
                return;
            }

            if (mountJoint != null && mountJoint.connectedBody == null)
            {
                Vector3 mountPoint = GetMountPoint();
                if (!hasWorldMountAnchor ||
                    (mountPoint - lastWorldMountAnchor).sqrMagnitude > 0.000001f)
                {
                    mountJoint.connectedAnchor = mountPoint;
                    lastWorldMountAnchor = mountPoint;
                    hasWorldMountAnchor = true;
                }
            }
        }

        private Vector3 GetMountPoint()
            => attachmentFrame != null ? attachmentFrame.TransformPoint(attachmentLocalPoint) : transform.position;

        private static Vector3 GetGravityDirection()
            => Physics.gravity.sqrMagnitude > 0.0001f ? Physics.gravity.normalized : Vector3.down;

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (layer < 0) return;
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        private static Material CreateMaterial(Color color, bool unlit)
        {
            Shader shader = Shader.Find(unlit
                ? "Universal Render Pipeline/Unlit"
                : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            Material material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.24f);
            return material;
        }

        private void OnDestroy()
        {
            if (history != null) history.Forget(this);
            DestroyRuntimeObject(chainMaterial);
            DestroyRuntimeObject(chainLinkMesh);
        }

        private static void DestroyRuntimeObject(Object instance)
        {
            if (instance == null) return;
            if (Application.isPlaying) Object.Destroy(instance);
            else Object.DestroyImmediate(instance);
        }
    }
}
