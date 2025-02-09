﻿using AutoMapper;
using DogWalkNationAPI.Models;
using DogWalkNationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DogWalkNationAPI.Helpers;

namespace DogWalkNationAPI.Controllers
{
    //Get mapper

    [ApiController]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private readonly ICosmosService<Models.User> _userHelper;
        private readonly IMapper _mapper;
        private readonly JwtService _jwtService;

        public UserController(CosmosClient dbClient, IMapper mapper, JwtService jwtService)
        {
            _userHelper = new CosmosService<Models.User>(dbClient, Models.User.ContainerName);
            _mapper = mapper;
            _jwtService = jwtService;
        }

        [TokenAuthorize]
        [HttpGet]
        [Route("/[controller]/allUsers")]
        public async Task<IActionResult> GetAllUsers()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            return Ok(await _userHelper.GetMultiple(query));
        }

        [TokenAuthorize]
        [HttpGet]
        [Route("/[controller]/{id}/{key}")]
        public async Task<IActionResult> GetUser(Guid id, string key)
        {
            return Ok(await _userHelper.Get(id, key));
        }

        [TokenAuthorize]
        [HttpPut]
        [Route("/[controller]/updateUser")]
        public async Task<Responses.Default> UpdateUser(Models.User user)
        {
            try
            {
                await _userHelper.Update(user.Username, user);
                return new Responses.Default() { Success = true, Message = "User updated successfully" };
            }
            catch (Exception)
            {
                return new Responses.Default() { Success = false, Message = "An error has occurred" };
                throw;
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("/[controller]/register")]
        public async Task<Responses.Default> RegisterUser(UserRegister newUser)
        {
            //Replace blank space and make email and username lower case
            newUser.Email = Regex.Replace(newUser.Email.ToLower(), @"\s+", "");
            newUser.Username = Regex.Replace(newUser.Username.ToLower(), @"\s+", "");

            // Check for existing user
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Email = @email OR c.UserHandle = @userHandle")
                .WithParameter("@email", newUser.Email)
                .WithParameter("@userHandle", newUser.Username);

            var existing = await _userHelper.GetMultiple(query);

            if (existing.Count() > 0)
            {
                var user = existing.FirstOrDefault();
                if (user.Username == newUser.Username && user.Email == newUser.Email)
                    return new Responses.Default() { Success = false, Message = "Email address and UserHandle already in use", StatusCode = HttpStatusCode.BadRequest };

                if (user.Username == newUser.Username)
                    return new Responses.Default() { Success = false, Message = "UserHandle already in use", StatusCode = HttpStatusCode.BadRequest };

                if (user.Email == newUser.Email)
                    return new Responses.Default() { Success = false, Message = "Email address already in use", StatusCode = HttpStatusCode.BadRequest };
            }

            // generate a 128-bit salt
            byte[] salt = new byte[128 / 8];
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetNonZeroBytes(salt);
            }

            //Hash the currently non hashed password
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: newUser.Password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            //Create userId
            Guid userId = Guid.NewGuid();

            //Create User object
            Models.User fullUser = new(userId, newUser.Username, newUser.Email, newUser.FirstName, newUser.LastName, salt, hashed);

            // Add New User to DB
            await _userHelper.Update(newUser.Username, fullUser);

            return new Responses.Default() { Success = true, Message = "User created" };
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("/[controller]/login")]
        public async Task<Responses.Default> Login(UserLogin userLogin)
        {
            userLogin.Email = Regex.Replace(userLogin.Email.ToLower(), @"\s+", "");

            //Grab user by email
            var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email")
                .WithParameter("@email", userLogin.Email);

            var userList = await _userHelper.GetMultiple(query);

            //Check if a user was found
            if (!userList.Any())
            {
                return new Responses.Default() { Success = false, Message = "Email address not found" };
            }
            else
            {
                var user = userList.FirstOrDefault();
                //Check password
                if (VerifyPassword(user, userLogin.Password) == true)
                {
                    var token = _jwtService.GenerateJwtToken(user);
                    return new Responses.DefaultWithUserAndToken() { Success = true, Message = "User found and logged in successfully", User = user, Token = token };
                }
                else
                {
                    return new Responses.Default() { Success = false, Message = "Incorrect password" };
                }
            }
        }

        [TokenAuthorize]
        [HttpPost]
        [Route("/[controller]/passwordchange")]
        public async Task<Responses.Default> ChangePassword(UserChangePassword user)
        {
            //Check password
            if (VerifyPassword(user, user.OldPassword) == true)
            {
                //Hash the new password
                string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: user.NewPassword,
                salt: user.Salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

                //Change users hashed password and update
                user.HashedPassword = hashed;

                //Map onto User model
                Models.User userUpdate = _mapper.Map<Models.User>(user);

                try
                {
                    await _userHelper.Update(userUpdate.Username, userUpdate);
                    return new Responses.Default() { Success = true, Message = "Password changed successfully" };
                }
                catch (CosmosException)
                {
                    return default;
                }
            }
            else
            {
                return new Responses.Default() { Success = false, Message = "Incorrect password" };
            }

        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public bool VerifyPassword(Models.User user, string password)
        {
            //Hash password with users salt
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: user.Salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            //Check against current hashed password
            if (hashed == user.HashedPassword)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
