﻿using FreeSql.DataAnnotations;
using System;

namespace OnceMi.IdentityServer4.User.Entities
{
    [Table(Name = nameof(UserToken))]
    [Index("index_{TableName}_" + nameof(UserId), nameof(UserId), true)]
    public class UserToken : IBaseEntity<long>
    {
        public long UserId { get; set; }

        /// <summary>
        /// Token
        /// </summary>
        [Column(StringLength = -1, IsNullable = false)]
        public string Token { get; set; }

        /// <summary>
        /// RefeshToken
        /// </summary>
        [Column(StringLength = 500, IsNullable = false)]
        public string RefeshToken { get; set; }

        /// <summary>
        /// RefeshToken到期时间
        /// </summary>
        [Column(IsNullable = false)]
        public DateTime RefeshTokenExpiration { get; set; }
    }
}
