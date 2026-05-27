using AssetManagementBase;
using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;
using System.IO;

namespace ThirdPersonTutorial;

public class ViewerGame : Game
{
	private const float MouseSensitivity = 0.2f;
	private const float MovementSpeed = 0.1f;
	private const float NearPlaneDistance = 0.1f;
	private const float FarPlaneDistance = 1000.0f;
	private const float ViewAngle = 60.0f;

	private readonly GraphicsDeviceManager _graphics;

	private BasicEffect _basicEffect;
	private Texture2D _textureField;
	private DrMesh _meshGround, _meshHero;
	private Vector3 _heroPosition, _heroRotation;
	private Vector3 _cameraMountRotation;
	private MouseState? _oldMouse = null;

	/// <summary>Initializes game with graphics and input configuration.</summary>
	public ViewerGame()
	{
		_graphics = new GraphicsDeviceManager(this)
		{
			PreferredBackBufferWidth = 1200,
			PreferredBackBufferHeight = 800
		};

		IsMouseVisible = true;
		Window.AllowUserResizing = true;
	}

	/// <summary>Loads all game content and scene setup.</summary>
	protected override void LoadContent()
	{
		base.LoadContent();

		var assetManager = AssetManager.CreateFileAssetManager(Path.Combine(AppContext.BaseDirectory, "Assets"));

		// Build scene hierarchy
		// Ground plane with checkerboard texture
		_textureField = assetManager.LoadTexture2D(GraphicsDevice, "Textures/checker.dds");
		_meshGround = MeshPrimitives.CreatePlaneMesh(GraphicsDevice, uScale: 50, vScale: 50, normalDirection: NormalDirection.UpY);

		_meshHero = MeshPrimitives.CreateCapsuleMesh(GraphicsDevice);

		_basicEffect = new BasicEffect(GraphicsDevice)
		{
			LightingEnabled = true
		};

		_basicEffect.DirectionalLight0.Enabled = true;
		_basicEffect.DirectionalLight0.Direction = new Vector3(-1, -1, -1);
		_basicEffect.DirectionalLight0.DiffuseColor = Color.White.ToVector3();
	}

	/// <summary>Updates game logic: input, animations, FPS counter.</summary>
	protected override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		var mouse = Mouse.GetState();

		if (_oldMouse != null)
		{
			var horizontalRotation = -(int)((mouse.X - _oldMouse.Value.X) * MouseSensitivity);
			_heroRotation.Y += horizontalRotation;

			var verticalRotation = -(int)((mouse.Y - _oldMouse.Value.Y) * MouseSensitivity);
			_cameraMountRotation.X += verticalRotation;
		}

		_oldMouse = mouse;

		/*		// Process WASD movement
				var isRunning = false;
				var velocity = Vector3.Zero;

				if (_inputService.IsKeyDown(Keys.W))
				{
					velocity = ModelNode.GlobalTransform.Forward * -MovementSpeed;
					isRunning = true;
				}
				else if (_inputService.IsKeyDown(Keys.S))
				{
					velocity = ModelNode.GlobalTransform.Forward * MovementSpeed;
					isRunning = true;
				}
				else if (_inputService.IsKeyDown(Keys.A))
				{
					velocity = ModelNode.GlobalTransform.Right * MovementSpeed;
					isRunning = true;
				}
				else if (_inputService.IsKeyDown(Keys.D))
				{
					velocity = ModelNode.GlobalTransform.Right * -MovementSpeed;
					isRunning = true;
				}

				if (_inputService.IsKeyDown(Keys.Space))
					_characterService.Jump(velocity);

				if (_inputService.IsKeyDown(Keys.LeftShift))
					_characterService.Slash();

				if (_inputService.IsKeyDown(Keys.R))
				{
					if (_characterService.WeaponDrawn)
						_characterService.SheathWeapon();
					else
						_characterService.DrawWeapon();
				}

				if (isRunning)
					_characterService.Run(velocity);
				else
					_characterService.Idle();

				_characterService.Update(gameTime.ElapsedGameTime);
				_fpsCounter.Update(gameTime);*/
	}

	private void DrawMesh(DrMesh mesh, Matrix world, Color color, Texture2D texture)
	{
		_basicEffect.DiffuseColor = color.ToVector3();
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

		_basicEffect.World = world;
		var device = GraphicsDevice;
		foreach (var part in mesh.MeshParts)
		{
			// Set vertex/index buffers
			device.SetVertexBuffer(part.VertexBuffer);
			device.Indices = part.IndexBuffer;

			// Render
			foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
			{
				pass.Apply();

				device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, part.PrimitiveCount);
			}
		}
	}

	/// <summary>Renders 3D scene and UI overlay.</summary>
	protected override void Draw(GameTime gameTime)
	{
		base.Draw(gameTime);

		var device = GraphicsDevice;

		device.Clear(Color.Black);

		// Set states
		device.DepthStencilState = DepthStencilState.Default;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.AlphaBlend;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		// Set projection
		var projection = Matrix.CreatePerspectiveFieldOfView(
				MathHelper.ToRadians(ViewAngle),
				device.Viewport.AspectRatio,
				NearPlaneDistance, FarPlaneDistance
			);
		_basicEffect.Projection = projection;

		// Firstly calculate the hero transform
		_heroPosition = new Vector3(0, 2, 0);
		var heroTransform = ToMatrix(_heroPosition, Vector3.One, _heroRotation);

		// Now the camera mount, which is attached to the hero head
		var cameraMountTransform = ToMatrix(new Vector3(0, 1.3f, 0), Vector3.One, _cameraMountRotation) * heroTransform;

		// Now the camera itself, which is attached to the camera mount with an offset
		var cameraTransform = Matrix.CreateTranslation(0, 0, -2) * cameraMountTransform;

		// Set view transform as the inverse of camera transform
		_basicEffect.View = Matrix.Invert(cameraTransform);

		// Draw ground
		DrawMesh(_meshGround, Matrix.CreateScale(200, 1, 200), Color.White, _textureField);

		// Draw hero
		DrawMesh(_meshHero, heroTransform, Color.Red, null);
	 }

	private static Matrix ToMatrix(Vector3 position, Vector3 scale, Vector3 rotationInDegrees)
	{
		var scaleTransform = Matrix.CreateScale(scale);
		var rotation = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(rotationInDegrees.Y), MathHelper.ToRadians(rotationInDegrees.X), MathHelper.ToRadians(rotationInDegrees.Z));
		var translation = Matrix.CreateTranslation(position);

		return scaleTransform * rotation * translation;
	}
}
