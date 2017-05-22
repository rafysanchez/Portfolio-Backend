﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Portfolio.API.Repositories;
using Portfolio.API.Models;
using Portfolio.API.Services;
using System.ComponentModel.DataAnnotations;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Portfolio.API.Controllers
{
    [Route("api/[controller]")]
    public class AccountController : Controller
    {
        private readonly IRepository<User> _userRepository;
        private readonly AuthenticationService _authenticationService;

        public AccountController(IRepository<User> userRepository)
        {
            _userRepository = userRepository;
            _authenticationService = new AuthenticationService(userRepository);
        }

        [HttpPost(Name = "CreateUser")]
        public IActionResult Create([FromBody] UserCreate item)
        {
            if (!AuthenticationService.EnableUserCreation)
                return BadRequest("User Creation is currently disabled by the administrator ");

            if (string.IsNullOrWhiteSpace(item.Username) || string.IsNullOrWhiteSpace(item.Email) || string.IsNullOrWhiteSpace(item.Password))
                return BadRequest("Please provide all Username, Email and Password");

            // Get the User and Authenticate
            User user = _userRepository.GetAllQuery()
                    .Where(x => x.Username.Equals(item.Username))
                    .FirstOrDefault();

            // If the user exists, ABORT!
            if (user != null)
                return BadRequest("This user already exists");

            // Validate the Inputs
            if (!_authenticationService.ValidateUsername(item.Username))
                return BadRequest("The username is invalid or has already been taken");
            if (!_authenticationService.ValidateEmail(item.Email))
                return BadRequest("The email is invalid");
            if (!_authenticationService.ValidatePassword(item.Password))
                return BadRequest("The password is invalid");

            user = new User();
            user.Username = item.Username;
            user.Email = item.Email;

            var userCount = _userRepository.Count + 1;
            user.Password_Hash = _authenticationService.HashPassword(userCount, item.Password);
            user.AuthToken = _authenticationService.GenerateAuthToken(userCount, item.Username);
            _userRepository.AddAndCommit(user);

            UserAuthenticated authenticatedUser = new UserAuthenticated();
            authenticatedUser.ID = user.ID;
            authenticatedUser.Username = user.Username;
            authenticatedUser.AuthToken = user.AuthToken;
            return new ObjectResult(authenticatedUser);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromHeader(Name = "Authorization")] string authToken, [FromBody] UserEdit item)
        {
            if (item == null)
                return BadRequest();
            if (string.IsNullOrWhiteSpace(item.Username) || string.IsNullOrWhiteSpace(item.Email) || string.IsNullOrWhiteSpace(item.CurrentPassword))
                return BadRequest("Please provide all Username, Email and Current Password");

            // Verify the Authorization Token
            if (!_authenticationService.VerifyAuthTokenAndID(id, authToken))
                return BadRequest("Invalid AuthToken");

            // Validate the Inputs
            if (!_authenticationService.ValidateUsername(item.Username))
                return BadRequest("The username is invalid, has already been taken or does not meet the minimum requirements");
            if (!_authenticationService.ValidateEmail(item.Email))
                return BadRequest("The email contains invalid characters or does not meet the minimum requirements");
            if (!_authenticationService.ValidatePassword(item.CurrentPassword))
                return BadRequest("The current password contains invalid characters or does not meet the minimum requirements");

            // Get the current user
            var repoItem = _userRepository.Find(id);
            if (repoItem == null)
                return NotFound();

            // Make sure the current password is correctly set as a third layer of security
            if (_authenticationService.VerifyPassword(item.CurrentPassword, repoItem.Password_Hash))
            {
                var userCount = _userRepository.Count + 1;
                if (!string.IsNullOrWhiteSpace(item.NewPassword))
                {
                    if (!_authenticationService.ValidatePassword(item.NewPassword))
                        return BadRequest("The new password contains invalid characters or does not meet the minimum requirements");

                    repoItem.Password_Hash = _authenticationService.HashPassword(userCount, item.NewPassword);
                    repoItem.AuthToken = _authenticationService.GenerateAuthToken(userCount, item.Username);
                }

                repoItem.Username = item.Username;
                repoItem.Email = item.Email;

                // Update the user
                _userRepository.UpdateAndCommit(repoItem);

                // Return the new authenticated user
                UserAuthenticated authenticatedUser = new UserAuthenticated();
                authenticatedUser.ID = repoItem.ID;
                authenticatedUser.Username = repoItem.Username;
                authenticatedUser.AuthToken = repoItem.AuthToken;
                return new ObjectResult(authenticatedUser);
            }

            return BadRequest("Current Password is incorrect");
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id, [FromHeader(Name = "Authorization")] string authToken)
        {
            if (!_authenticationService.VerifyAuthTokenAndID(id, authToken))
                return BadRequest("Invalid AuthToken");

            var repoItem = _userRepository.Find(id);
            if (repoItem == null)
                return NotFound();

            _userRepository.RemoveAndCommit(id);
            return new NoContentResult();
        }
    }
}
