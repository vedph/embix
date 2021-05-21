-- token

CREATE TABLE `token` (
  `id` int NOT NULL AUTO_INCREMENT,
  `value` varchar(100) NOT NULL,
  `language` varchar(5) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `token_targetId_IDX` (`targetId`) USING BTREE,
  KEY `token_value_IDX` (`value`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- occurrence

CREATE TABLE `occurrence` (
  `id` int NOT NULL AUTO_INCREMENT,
  `tokenId` int NOT NULL,
  `field` char(5) NOT NULL,
  `targetId` varchar(20) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `occurrence_FK` (`tokenId`),
  KEY `occurrence_field_IDX` (`field`) USING BTREE,
  CONSTRAINT `occurrence_FK` FOREIGN KEY (`tokenId`) REFERENCES `token` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
