﻿using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OnceMi.Framework.IService.Admin;
using OnceMi.Framework.Model.Dto;
using OnceMi.Framework.Model.Enums;
using OnceMi.Framework.Model.Exception;
using OnceMi.Framework.Util.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace OnceMi.Framework.Api.Controllers.v1.Admin
{
    /// <summary>
    /// 视图管理
    /// </summary>
    [ApiController]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class ViewController : ControllerBase
    {
        private readonly ILogger<ViewController> _logger;
        private readonly IViewsService _service;
        private readonly IMapper _mapper;

        public ViewController(ILogger<ViewController> logger
            , IViewsService service
            , IMapper mapper)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// 查询级联选择器数据
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("[action]")]
        public async Task<List<ICascaderResponse>> CascaderList()
        {
            var data = await _service.Query(new IPageRequest()
            {
                Page = 1,
                Size = 999999,
                OrderBy = new string[] { "id,asc" },
            }, true);
            if (data != null && data.PageData != null && data.PageData.Any())
            {
                return _mapper.Map<List<ICascaderResponse>>(data.PageData);
            }
            return new List<ICascaderResponse>();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IPageResponse<ViewItemResponse>> Get([FromQuery] IPageRequest request)
        {
            return await _service.Query(request);
        }

        /// <summary>
        /// 根据ID查询
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<ViewItemResponse> Get(long id)
        {
            return await _service.Query(id);
        }

        /// <summary>
        /// 新增视图
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ViewItemResponse> Post(CreateViewRequest request)
        {
            if (!string.IsNullOrEmpty(request.Query))
            {
                if (request.Query[0] != '{' || request.Query[^0] != '}')
                {
                    throw new BusException(-1, "参数必须是合法的Json字符串");
                }
                try
                {
                    var (isJson, json) = JsonUtil.IsJson(request.Query);
                    if (isJson)
                    {
                        throw new BusException(-1, "参数必须是合法的Json字符串");
                    }
                    request.Query = json;
                }
                catch
                {
                    throw new BusException(-1, "参数必须是合法的Json字符串");
                }
            }

            return await _service.Insert(request);
        }

        /// <summary>
        /// 修改视图
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task Put(UpdateViewRequest request)
        {
            if (!string.IsNullOrEmpty(request.Query))
            {
                if (request.Query[0] != '{' || request.Query[^1] != '}')
                {
                    throw new BusException(-1, "参数必须是合法的Json字符串");
                }
                try
                {
                    object queryObj = JsonUtil.DeserializeStringToObject<JsonElement>(request.Query);
                    if (queryObj == null)
                    {
                        throw new BusException(-1, "参数必须是合法的Json字符串");
                    }
                    request.Query = JsonUtil.SerializeToString(queryObj);
                }
                catch
                {
                    throw new BusException(-1, "参数必须是合法的Json字符串");
                }
            }

            await _service.Update(request);
        }

        /// <summary>
        /// 根据Id删除
        /// </summary>
        [HttpDelete]
        public async Task Delete(List<long> ids)
        {
            await _service.Delete(ids);
        }
    }
}
