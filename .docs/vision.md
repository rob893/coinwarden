# Executive Summary

## Overview

Coinwarden is a web application designed to help players understand, monitor, and capitalize on the World of Warcraft economy. The platform collects auction house data from Blizzard APIs, maintains historical pricing information, and provides market intelligence tools for traders, crafters, gatherers, and gold-makers.

Rather than focusing solely on current prices, the application emphasizes trends, profitability, inventory valuation, and actionable insights.

## Core Features

### Historical Price Tracking

The platform continuously records auction house data and presents historical trends through interactive charts. Users can view hourly, daily, and long-term price movements for items, commodities, and materials while analyzing supply levels and market volatility.

### Realm and Region Comparison

Players can compare prices across multiple realms and regions to identify market disparities and better understand local economies.

### Watchlists and Price Alerts

Users can create personalized watchlists and receive notifications when:

- Prices fall below a target value
- Prices exceed specified thresholds
- Supply becomes scarce
- Significant market movements occur

Alerts can be delivered through email, Discord webhooks, or mobile notifications.

### Market Movers

The application highlights:

- Largest gainers
- Largest losers
- Price spikes
- Market crashes
- Volume anomalies

This provides a quick overview of changing market conditions and emerging opportunities.

## Goldmaking Features

### Crafting Profitability

For professions such as Alchemy, Blacksmithing, Enchanting, and Tailoring, the platform calculates:

- Material costs
- Finished item values
- Auction House fees
- Expected profit
- Profit margins

This enables players to identify the most profitable recipes and production opportunities.

### Gathering Analytics

Gatherers can monitor current material prices and estimate gold-per-hour opportunities for mining, herbalism, skinning, and fishing.

### Flip Finder

The system identifies potential arbitrage opportunities by comparing current prices with historical averages and highlighting undervalued items with sufficient trading volume.

## Portfolio and Inventory Tracking

The application treats player inventories similarly to investment portfolios.

Users can:

- Maintain holdings manually or through addon synchronization
- Track inventory value over time
- Monitor gains and losses
- Identify concentration risks
- Analyze portfolio performance

The result is a "personal finance dashboard for World of Warcraft."

## Market Intelligence

The platform generates insights based on statistical analysis and game events, including:

- Patch releases
- Raid openings
- Season launches
- Supply shortages
- Sudden demand spikes

Users receive explanations for unusual market behavior rather than simply viewing price charts.

## Recipe and Resource Planning

Players can input available materials and receive recommendations on which items to craft to maximize profit. This enables efficient use of inventory and simplifies production planning.

## Technology Platform

The application is built using:

- ASP.NET Core and .NET
- PostgreSQL for historical time-series storage
- Hangfire for background processing
- React and TypeScript for the user interface
- Blizzard APIs for auction data
- Azure-hosted infrastructure

The architecture is designed to scale from a personal hobby project into a production service while maintaining low operating costs.

## Vision

"Monarch Money meets Bloomberg Terminal for WoW."

The long-term vision is to provide players with the equivalent of a modern financial analytics platform for the World of Warcraft economy—combining historical data, portfolio tracking, profitability analysis, and intelligent market insights into a single experience for casual players and dedicated gold-makers alike.
