using AssetManagementBase;
using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;

namespace ThirdPersonTutorial;

public class ViewerGame : Game
{
	// Camera control: scales mouse delta to rotation change (higher = more sensitive)
	private const float MouseSensitivity = 0.2f;
	// Character movement: distance units per frame when moving
	private const float MovementSpeed = 0.1f;
	// Camera frustum: closest object that renders
	private const float NearPlaneDistance = 0.1f;
	// Camera frustum: farthest object that renders
	private const float FarPlaneDistance = 1000.0f;
	// Camera field of view in degrees
	private const float ViewAngle = 60.0f;
	// Default height for the hero (keeps them above ground plane)
	private const float DefaultY = 1;
	// Physics gravity: acceleration downward per frame while jumping
	private const float Gravity = 0.015f;
	// Initial upward velocity when jumping
	private const float JumpForce = 0.5f;

	private readonly GraphicsDeviceManager _graphics;

	// Rendering
	private BasicEffect _basicEffect;
	private Texture2D _textureField;
	private DrMesh _meshGround, _meshHero;

	// Hero state: position and rotation in world space
	private Vector3 _heroPosition, _heroRotation;
	// Camera rotation relative to hero (pitch and yaw for head look)
	private Vector3 _cameraMountRotation;

	// Input tracking for mouse delta calculation
	private MouseState? _oldMouse = null;

	// Jump physics state
	private bool _isJumping = false;
	private float _jumpVelocity;
	private Vector3 _jumpMovement;

	/// <summary>Initializes the game with graphics and input configuration.</summary>
	public ViewerGame()
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

	/// <summary>Loads all game content and scene setup.</summary>
	protected override void LoadContent()
	{
		base.LoadContent();

		var assetManager = AssetManager.CreateFileAssetManager(Path.Combine(AppContext.BaseDirectory, "Assets"));

		// Load checkerboard texture for the ground
		_textureField = assetManager.LoadTexture2D(GraphicsDevice, "Textures/checker.dds");
		// Create a large ground plane (50x50 uv scales) with 200x200 world scale applied later
		_meshGround = MeshPrimitives.CreatePlaneMesh(GraphicsDevice, uScale: 50, vScale: 50, normalDirection: NormalDirection.UpY);

		// Create a capsule mesh for the hero character (simple geometric shape)
		_meshHero = MeshPrimitives.CreateCapsuleMesh(GraphicsDevice);

		// Configure the basic effect with lighting
		_basicEffect = new BasicEffect(GraphicsDevice)
		{
			LightingEnabled = true
		};

		// Set up a directional light from above-left-front
		_basicEffect.DirectionalLight0.Enabled = true;
		_basicEffect.DirectionalLight0.Direction = new Vector3(-1, -1, -1);
		_basicEffect.DirectionalLight0.DiffuseColor = Color.White.ToVector3();

		// Initialize hero in the center of the world at default height
		_heroPosition = new Vector3(0, DefaultY, 0);
	}

	/// <summary>Initiates a jump with optional forward momentum.</summary>
	/// <param name="movement">World-space velocity to apply during the jump arc</param>
	private void Jump(Vector3 movement)
	{
		// Prevent double-jumping mid-air
		if (_isJumping)
		{
			return;
		}

		// Set initial upward velocity
		_jumpVelocity = JumpForce;
		// Preserve horizontal momentum from movement input during jump
		_jumpMovement = movement;

		_isJumping = true;
	}

	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		// --- Input handling: Mouse ---
		var mouse = Mouse.GetState();

		if (_oldMouse != null)
		{
			// Calculate horizontal (yaw) rotation from mouse X delta
			var horizontalRotation = -(int)((mouse.X - _oldMouse.Value.X) * MouseSensitivity);
			_heroRotation.Y += horizontalRotation;

			// Calculate vertical (pitch) rotation from mouse Y delta
			var verticalRotation = -(int)((mouse.Y - _oldMouse.Value.Y) * MouseSensitivity);
			_cameraMountRotation.X += verticalRotation;

			// Clamp vertical look angle to prevent looking too far up/down (5-90 degrees)
			_cameraMountRotation.X = MathHelper.Clamp(_cameraMountRotation.X, 5, 90);
		}

		_oldMouse = mouse;

		// --- Movement and jumping logic ---
		if (!_isJumping)
		{
			// Handle ground-based movement (WASD)
			var velocity = Vector3.Zero;
			// Get hero's local transform to calculate forward/right directions
			var heroTransform = ToMatrix(_heroPosition, Vector3.One, _heroRotation);
			var keyboard = Keyboard.GetState();

			// Movement is relative to hero's facing direction
			if (keyboard.IsKeyDown(Keys.W))
			{
				velocity = heroTransform.Forward * -MovementSpeed;
			}
			else if (keyboard.IsKeyDown(Keys.S))
			{
				velocity = heroTransform.Forward * MovementSpeed;
			}
			else if (keyboard.IsKeyDown(Keys.A))
			{
				velocity = heroTransform.Right * MovementSpeed;
			}
			else if (keyboard.IsKeyDown(Keys.D))
			{
				velocity = heroTransform.Right * -MovementSpeed;
			}

			_heroPosition += velocity;

			// Jump with current movement velocity to maintain momentum
			if (keyboard.IsKeyDown(Keys.Space))
			{
				Jump(velocity);
			}
		}
		else
		{
			// Update jump state: apply gravity and move along jump trajectory
			_jumpVelocity -= Gravity;
			_heroPosition.Y += _jumpVelocity;
			_heroPosition += _jumpMovement;

			// Landing detection: stop jumping when feet reach ground level
			if (_heroPosition.Y <= DefaultY)
			{
				_heroPosition.Y = DefaultY;
				_isJumping = false;
			}
		}
	}

	/// <summary>Renders a mesh with the specified world transform, color tint, and optional texture.</summary>
	private void DrawMesh(DrMesh mesh, Matrix world, Color color, Texture2D texture)
	{
		// Apply color tint to the mesh
		_basicEffect.DiffuseColor = color.ToVector3();

		// Apply optional texture; if null, render with solid color only
		if (texture != null)
		{
			_basicEffect.TextureEnabled = true;
			_basicEffect.Texture = texture;
		}
		else
		{
			_basicEffect.TextureEnabled = false;
			_basicEffect.Texture = null;
		}

		// Set world transformation matrix
		_basicEffect.World = world;
		var device = GraphicsDevice;

		// Draw each part of the mesh (may have multiple parts for complex models)
		foreach (var part in mesh.MeshParts)
		{
			// Bind vertex and index buffers for this mesh part
			device.SetVertexBuffer(part.VertexBuffer);
			device.Indices = part.IndexBuffer;

			// Execute rendering passes (typically one pass for BasicEffect)
			foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
			{
				pass.Apply();
				// Draw using indexed primitives (indices define triangle connectivity)
				device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.PrimitiveCount);
			}
		}
	}

	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		var device = GraphicsDevice;

		// Clear the backbuffer with black
		device.Clear(Color.Black);

		// Set up GPU rendering state
		device.DepthStencilState = DepthStencilState.Default;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.AlphaBlend;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		// Set up camera projection matrix (perspective field of view)
		var projection = Matrix.CreatePerspectiveFieldOfView(
				MathHelper.ToRadians(ViewAngle),
				device.Viewport.AspectRatio,
				NearPlaneDistance, FarPlaneDistance
			);
		_basicEffect.Projection = projection;

		// --- Hierarchical transform system for camera ---
		// 1. Hero body: positioned in world, rotated by player input (YAW only)
		var heroTransform = ToMatrix(_heroPosition, Vector3.One, _heroRotation);

		// 2. Camera mount: represents "head" position (1 unit above feet) and pitch from mouse Y
		//    This is attached to the hero body, so it inherits hero rotation
		var cameraMountTransform = ToMatrix(new Vector3(0, 1f, 0), Vector3.One, _cameraMountRotation) * heroTransform;

		// 3. Camera: positioned 5 units behind the head, rotated 180° to look forward
		//    This is attached to the camera mount, so it inherits all parent rotations
		var cameraTransform = ToMatrix(new Vector3(0, 0, -5), Vector3.One, new Vector3(0, 180, 0)) * cameraMountTransform;

		// Convert camera transform to view matrix (inverse of camera position/rotation)
		_basicEffect.View = Matrix.Invert(cameraTransform);

		// Draw the ground plane (scaled 200x200 world units)
		DrawMesh(_meshGround, Matrix.CreateScale(200, 1, 200), Color.White, _textureField);

		// Draw the hero capsule at its current position and rotation
		DrawMesh(_meshHero, heroTransform, Color.Green, null);
	}

	/// <summary>
	/// Converts position, scale, and rotation into a transformation matrix.
	/// Order: Scale -> Rotate -> Translate (standard TRS order)
	/// </summary>
	/// <param name="position">World position</param>
	/// <param name="scale">Uniform scale factors</param>
	/// <param name="rotationInDegrees">Rotation in degrees (X=pitch, Y=yaw, Z=roll)</param>
	private static Matrix ToMatrix(Vector3 position, Vector3 scale, Vector3 rotationInDegrees)
	{
		var scaleTransform = Matrix.CreateScale(scale);
		var rotation = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(rotationInDegrees.Y), MathHelper.ToRadians(rotationInDegrees.X), MathHelper.ToRadians(rotationInDegrees.Z));
		var translation = Matrix.CreateTranslation(position);

		return scaleTransform * rotation * translation;
	}
}
