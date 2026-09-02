## Identified domains

- `Messaging`: sending, storing, editing, and retrieving messages.
- `Channel management`: channels, memberships, and permissions.
- `User profiles`: user information and profile settings.
- `Engagement`: reactions, mentions, and notifications.

## Boundaries

- `Messaging` owns message lifecycle, but not channel permissions or profile data.
- `Channel management` owns channels, memberships, and permissions.
- `User profiles` owns profile data.
- `Engagement` owns reactions, mentions, and notifications, but not message storage.
- Each boundary owns its own data, and communication should go through APIs or events rather than a shared database.

## Proposed services

- `Message Service`: implements the `Messaging` boundary.
- `Channel Service`: implements the `Channel management` boundary.
- `User Profile Service`: implements the `User profiles` boundary.
- `Engagement Service`: implements the `Engagement` boundary.

## Interactions

- `Message Service` asks `Channel Service` whether a user can post in a channel.
- `Message Service` stores the message and publishes `MessageCreated`.
- `Engagement Service` listens for `MessageCreated` and handles mentions or notifications.

## Container diagram

![Bizcord backend container diagram](<../../img/Container diagram Jeppe.png>)
