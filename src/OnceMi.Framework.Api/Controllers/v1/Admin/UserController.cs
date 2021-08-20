﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OnceMi.Framework.IService.Admin;
using OnceMi.Framework.Model.Dto;
using OnceMi.Framework.Model.Enums;
using OnceMi.Framework.Model.Exception;
using OnceMi.IdentityServer4.User.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OnceMi.Framework.Api.Controllers.v1.Admin
{
    /// <summary>
    /// 用户管理
    /// </summary>
    [ApiController]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly IUsersService _service;
        private readonly IMapper _mapper;

        public UserController(ILogger<UserController> logger
            , IUsersService service
            , IMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }
        /// <summary>
        /// 获取用户状态
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("[action]")]
        public async Task<List<ISelectResponse<string>>> UserStatusSelectList()
        {
            return await _service.GetUserStatus();
        }

        /// <summary>
        /// 查询用户性别
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("[action]")]
        public async Task<List<ISelectResponse<string>>> UserGenderSelectList()
        {
            return await _service.GetUserGender();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IPageResponse<UserItemResponse>> Get([FromQuery] QueryUserPageRequest request)
        {
            return await _service.Query(request);
        }

        /// <summary>
        /// 查询用户信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<UserItemResponse> Get(long id)
        {
            var user = await _service.Query(id.ToString(), false);
            return _mapper.Map<UserItemResponse>(user);
        }

        /// <summary>
        /// 新增用户
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<UserItemResponse> Post(CreateUserRequest request)
        {
            if (string.IsNullOrEmpty(request.PhoneNumber))
            {
                request.PhoneNumberConfirmed = false;
            }
            if (string.IsNullOrEmpty(request.Email))
            {
                request.EmailConfirmed = false;
            }
            if (!UserHelpers.IsUserName(request.UserName))
            {
                throw new BusException(-1, "用户名只能由数字和字母组成");
            }
            return await _service.Insert(request);
        }

        /// <summary>
        /// 修改用户
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task Put(UpdateUserRequest request)
        {
            if (string.IsNullOrEmpty(request.PhoneNumber))
            {
                request.PhoneNumberConfirmed = false;
            }
            if (string.IsNullOrEmpty(request.Email))
            {
                request.EmailConfirmed = false;
            }
            if (!UserHelpers.IsUserName(request.UserName))
            {
                throw new BusException(-1, "用户名只能由数字和字母组成");
            }
            await _service.Update(request);
        }

        /// <summary>
        /// 修改用户状态
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("UpdateUserStatus")]
        public async Task Put(UpdateUserStatusRequest request)
        {
            await _service.UpdateUserStatus(request);
        }

        /// <summary>
        /// 修改用户密码
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("UpdateUserPassword")]
        public async Task Put(UpdateUserPasswordRequest request)
        {
            await _service.UpdateUserPassword(request);
        }

        /// <summary>
        /// 根据Id删除
        /// </summary>
        [HttpDelete]
        public async Task Delete(List<long> ids)
        {
            await _service.Delete(ids);
        }

        /// <summary>
        /// 获取随机头像
        /// </summary>
        /// <param name="name"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("[action]/{name}")]
        [AllowAnonymous]
        public async Task<IActionResult> Avatar(string name, int size)
        {
            byte[] avatarBytes = await _service.GetAvatar(name, size);
            return File(new MemoryStream(avatarBytes), "image/png", "avatar.png");
        }
    }
}
