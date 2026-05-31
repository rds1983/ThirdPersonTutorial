using AssetManagementBase;
using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

namespace ThirdPersonTutorial;

public class MyGame : Game
{
	// Mouse look sensitivity multiplier
	private const float MouseSensitivity = 0.2f;
	// Movement speed per frame
	private const float MovementSpeed = 0.1f;
	// Camera near clipping plane
	private const float NearPlaneDistance = 0.1f;
	// Camera far clipping plane
	private const float FarPlaneDistance = 1000.0f;
	// Camera field of view in degrees
	private const float ViewAngle = 60.0f;
	// Hero ground height
	private const float DefaultY = 1;
	// Jump gravity acceleration per second
	private const float Gravity = 12f;
	// Jump initial velocity
	private const float JumpForce = 10f;

	private readonly GraphicsDeviceManager _graphics;

	// Stock effect with directional lighting and texturing
	private BasicEffect _basicEffect;

	// Ground plane texture
	private Texture2D _textureGround;

	// Ground plane mesh
	private DrMesh _meshGround;

	// Capsule mesh for the player
	private DrMesh _meshHero;

	// Hero position in world space
	private Vector3 _heroPosition;

	// Hero body yaw rotation in degrees
	private float _heroYaw;

	// Camera mount pitch rotation in degrees
	private float _cameraMountPitch;

	// Previous mouse state for delta calculation
	private MouseState? _oldMouse = null;

	// Jump state and physics
	private DateTime? _jumpStarted;
	private Vector3 _jumpMovement;

	/// <summary>Initializes the game with graphics and input configuration.</summary>
	public MyGame()
	{
		// Set up graphics device with preferred window resolution
		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1200,
			PreferredBackBufferHeight = 800
		};

		// Show the mouse cursor for this UI
		IsMouseVisible = true;
		// Allow the player to resize the window
		Window.AllowUserResizing = true;
	}

	protected override void LoadContent()
	{
		base.LoadContent();

		// Load ground texture
		var assetManager = AssetManager.CreateFileAssetManager(Path.Combine(AppContext.BaseDirectory, "Assets"));
		_textureGround = assetManager.LoadTexture2D(GraphicsDevice, "Textures/checker.dds");

		// Create ground and hero meshes
		_meshGround = MeshPrimitives.CreatePlaneMesh(GraphicsDevice, uScale: 50, vScale: 50, normalDirection: NormalDirection.UpY);
		_meshHero = MeshPrimitives.CreateCapsuleMesh(GraphicsDevice);

		// Set up rendering effect with lighting
		_basicEffect = new BasicEffect(GraphicsDevice) { LightingEnabled = true };
		_basicEffect.DirectionalLight0.Enabled = true;
		_basicEffect.DirectionalLight0.Direction = new Vector3(-1, -1, -1);
		_basicEffect.DirectionalLight0.DiffuseColor = Color.White.ToVector3();

		// Start hero at world center
		_heroPosition = new Vector3(0, DefaultY, 0);
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		// Handle mouse input for camera rotation
		var mouse = Mouse.GetState();

		if (_oldMouse != null)
		{
			// Rotate hero by mouse X delta
			var horizontalRotation = -(int)((mouse.X - _oldMouse.Value.X) * MouseSensitivity);
			_heroYaw += horizontalRotation;

			// Tilt camera by mouse Y delta
			var verticalRotation = -(int)((mouse.Y - _oldMouse.Value.Y) * MouseSensitivity);
			_cameraMountPitch += verticalRotation;

			// Clamp pitch to valid range (-20 to 70 degrees)
			_cameraMountPitch = MathHelper.Clamp(_cameraMountPitch, -20, 70);
		}

		_oldMouse = mouse;

		// Handle movement and jumping
		if (_jumpStarted == null)
		{
			// WASD movement
			var velocity = Vector3.Zero;
			var heroTransform = ToMatrix(_heroPosition, Vector3.One, _heroYaw, 0, 0);
			var keyboard = Keyboard.GetState();

			if (keyboard.IsKeyDown(Keys.W))
				velocity = heroTransform.Forward * -MovementSpeed;
			else if (keyboard.IsKeyDown(Keys.S))
				velocity = heroTransform.Forward * MovementSpeed;
			else if (keyboard.IsKeyDown(Keys.A))
				velocity = heroTransform.Right * MovementSpeed;
			else if (keyboard.IsKeyDown(Keys.D))
				velocity = heroTransform.Right * -MovementSpeed;

			_heroPosition += velocity;

			if (keyboard.IsKeyDown(Keys.Space))
			{
				// Jump
				_jumpStarted = DateTime.Now;
				_jumpMovement = velocity;
			}
		}
		else
		{
			// When moving with acceleration
			// Formula for the jump height: h = h0 + v0 * t - 0.5 * g * t^2
			// Where h0 is the initial height(DefaultY), v0 is the initial jump velocity(JumpForce), g is the gravity(JumpGravity), and t is the time passed since jump started

			var t = (float)(DateTime.Now - _jumpStarted.Value).TotalSeconds;

			var jumpHeight = DefaultY + (JumpForce * t) - (0.5f * Gravity * t * t);
			_heroPosition.Y = jumpHeight;
			_heroPosition += _jumpMovement;

			// Land when reaching ground
			if (_heroPosition.Y <= DefaultY)
			{
				_heroPosition.Y = DefaultY;
				_jumpStarted = null;
			}
		}
	}

	/// <summary>Render a mesh with color and texture.</summary>
	private void DrawMesh(DrMesh mesh, Matrix world, Color color, Texture2D texture)
	{
		_basicEffect.DiffuseColor = color.ToVector3();
		_basicEffect.TextureEnabled = texture != null;
		_basicEffect.Texture = texture;
		_basicEffect.World = world;

		var device = GraphicsDevice;
		foreach (var part in mesh.MeshParts)
		{
			device.SetVertexBuffer(part.VertexBuffer);
			device.Indices = part.IndexBuffer;

			foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
			{
				pass.Apply();
				device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.PrimitiveCount);
			}
		}
	}

	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		var device = GraphicsDevice;
		device.Clear(Color.Black);

		// Set GPU states
		device.DepthStencilState = DepthStencilState.Default;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.AlphaBlend;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		// Set projection
		var projection = Matrix.CreatePerspectiveFieldOfView(
			MathHelper.ToRadians(ViewAngle),
			device.Viewport.AspectRatio,
			NearPlaneDistance, FarPlaneDistance);
		_basicEffect.Projection = projection;

		// Build camera hierarchy: hero body -> camera mount (head) -> camera
		var heroTransform = ToMatrix(_heroPosition, Vector3.One, _heroYaw, 0, 0);
		var cameraMountTransform = ToMatrix(new Vector3(0, 1f, 0), Vector3.One, 0, _cameraMountPitch, 0) * heroTransform;
		var cameraTransform = ToMatrix(new Vector3(0, 0, -5), Vector3.One, 180, 0, 0) * cameraMountTransform;

		_basicEffect.View = Matrix.Invert(cameraTransform);

		// Draw ground and hero
		DrawMesh(_meshGround, Matrix.CreateScale(200, 1, 200), Color.White, _textureGround);
		DrawMesh(_meshHero, heroTransform, Color.Green, null);
	}

	/// <summary>Build transform matrix from position, scale, and rotation (TRS order).</summary>
	private static Matrix ToMatrix(Vector3 position, Vector3 scale, float yaw, float pitch, float roll)
	{
		var scaleTransform = Matrix.CreateScale(scale);
		var rotation = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(yaw), MathHelper.ToRadians(pitch), MathHelper.ToRadians(roll));
		var translation = Matrix.CreateTranslation(position);

		return scaleTransform * rotation * translation;
	}
}
