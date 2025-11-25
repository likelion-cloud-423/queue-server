FROM eclipse-temurin:17-jdk-alpine AS build
WORKDIR /workspace

# Copy Gradle wrapper and shared build metadata
COPY gradlew gradlew
COPY gradle gradle
COPY settings.gradle settings.gradle
COPY build.gradle build.gradle

# Copy module sources
COPY queue-manager queue-manager
COPY queue-api queue-api

RUN chmod +x gradlew && ./gradlew :queue-manager:bootJar --no-daemon

FROM eclipse-temurin:17-jre-alpine AS runtime
WORKDIR /app
COPY --from=build /workspace/queue-manager/build/libs/queue-manager.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java","-jar","app.jar"]
